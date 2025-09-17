using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Models;
using Avalonia.Threading;
using skt.IDE.Services;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class FileExplorerViewModel : ViewModelBase
{
    private const String NoProjectName = "Open A Project to Begin";
    
    [ObservableProperty]
    private ObservableCollection<FileNode> _rootNodes = new();

    [ObservableProperty]
    private string _projectName = NoProjectName;

    private string _currentProjectPath = string.Empty;

    public FileExplorerViewModel()
    {
        ProjectName = NoProjectName;
        App.EventBus.Subscribe<FileCreatedEvent>(OnFileCreated);
        App.EventBus.Subscribe<FileUpdatedEvent>(OnFileUpdated);
    }

    private async void OnFileCreated(FileCreatedEvent fileEvent)
    {
        System.Diagnostics.Debug.WriteLine($"FileExplorerViewModel received FileCreatedEvent for: {fileEvent.FilePath}");
        System.Diagnostics.Debug.WriteLine($"Current project path: {_currentProjectPath}");

        if (string.IsNullOrEmpty(_currentProjectPath) || !fileEvent.FilePath.StartsWith(_currentProjectPath))
        {
            System.Diagnostics.Debug.WriteLine("File is not in current project - ignoring event");
            return;
        }

        System.Diagnostics.Debug.WriteLine("Refreshing file tree...");
        // Use a simple approach: just refresh the entire file tree
        // This is more reliable than trying to navigate the hierarchy
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await LoadProject(_currentProjectPath);
        });
        System.Diagnostics.Debug.WriteLine("File tree refresh completed");
    }

    private async void OnFileUpdated(FileUpdatedEvent fileEvent)
    {
        System.Diagnostics.Debug.WriteLine($"FileExplorerViewModel received FileUpdatedEvent for: {fileEvent.FilePath}");
        System.Diagnostics.Debug.WriteLine($"Current project path: {_currentProjectPath}");

        if (string.IsNullOrEmpty(_currentProjectPath) || !fileEvent.FilePath.StartsWith(_currentProjectPath))
        {
            System.Diagnostics.Debug.WriteLine("File is not in current project - ignoring event");
            return;
        }

        System.Diagnostics.Debug.WriteLine("Refreshing file tree due to file update...");
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await LoadProject(_currentProjectPath);
        });
        System.Diagnostics.Debug.WriteLine("File tree refresh completed after update");
    }

    public async Task LoadProject(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
        {
            ProjectName = NoProjectName;
            _currentProjectPath = string.Empty;
            return;
        }

        _currentProjectPath = projectPath;
        List<FileNode> childNodes = new();
        string projectName = Path.GetFileName(projectPath);

        // Ensure we have a valid project name
        if (string.IsNullOrEmpty(projectName))
        {
            projectName = Path.GetFileName(Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        // Build the file tree off the UI thread
        await Task.Run(() =>
        {
            try
            {
                // Load directories first
                foreach (var dir in Directory.GetDirectories(projectPath))
                {
                    childNodes.Add(new FileNode(dir));
                }

                // Load files
                foreach (var file in Directory.GetFiles(projectPath))
                {
                    childNodes.Add(new FileNode(file));
                }
            }
            catch
            {
                // ignore IO errors
            }
        });

        // Apply changes on the UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RootNodes.Clear();
            foreach (var node in childNodes)
            {
                RootNodes.Add(node);
            }
            ProjectName = string.IsNullOrEmpty(projectName) ? NoProjectName : projectName;
        });

        App.EventBus.Publish(new ProjectLoadedEvent(projectPath));
    }

    private async Task RefreshFileTree()
    {
        if (!string.IsNullOrEmpty(_currentProjectPath))
        {
            await LoadProject(_currentProjectPath);
        }
    }

    // Event to notify when a file is selected
    public event Action<string>? FileSelected;

    internal void NotifyFileSelected(string filePath)
    {
        FileSelected?.Invoke(filePath);
    }
}
