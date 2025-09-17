using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Models;
using Avalonia.Threading;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class FileExplorerViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<FileNode> _rootNodes = new();

    [ObservableProperty]
    private string _projectName = "No Project";

    public FileExplorerViewModel()
    {
        ProjectName = "No Project";
    }

    public async Task LoadProject(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            return;

        FileNode? rootNode = null;
        string projectName = Path.GetFileName(projectPath);

        // Build the file tree off the UI thread
        await Task.Run(() =>
        {
            try
            {
                rootNode = new FileNode(projectPath)
                {
                    IsExpanded = true
                };
            }
            catch
            {
                rootNode = null;
            }
        });

        if (rootNode is null)
            return;

        // Apply changes on the UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RootNodes.Clear();
            RootNodes.Add(rootNode);
            ProjectName = string.IsNullOrEmpty(projectName) ? "No Project" : projectName;
        });
    }

    // Event to notify when a file is selected
    public event Action<string>? FileSelected;

    internal void NotifyFileSelected(string filePath)
    {
        FileSelected?.Invoke(filePath);
    }
}
