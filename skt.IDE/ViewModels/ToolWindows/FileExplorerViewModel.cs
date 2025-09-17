using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Models;
using Avalonia.Threading;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class FileExplorerViewModel : ViewModelBase
{
    private const String NoProjectName = "Open A Project to Begin";
    
    [ObservableProperty]
    private ObservableCollection<FileNode> _rootNodes = new();

    [ObservableProperty]
    private string _projectName = NoProjectName;

    public FileExplorerViewModel()
    {
        ProjectName = NoProjectName;
    }

    public async Task LoadProject(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
        {
            ProjectName = NoProjectName;
            return;
        }

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
    }

    // Event to notify when a file is selected
    public event Action<string>? FileSelected;

    internal void NotifyFileSelected(string filePath)
    {
        FileSelected?.Invoke(filePath);
    }
}
