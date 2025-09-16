using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using skt.IDE.Models;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class FileExplorerViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<FileNode> _rootNodes = new();

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private string _projectName = "No Project";

    public FileExplorerViewModel()
    {
        // Start with an empty explorer
        ProjectName = "No Project";
    }

    public async Task LoadProject(string projectPath)
    {
        await Task.Run(() => LoadPath(projectPath));
        ProjectName = Path.GetFileName(projectPath);
    }

    [RelayCommand]
    public void LoadPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        CurrentPath = path;
        RootNodes.Clear();

        try
        {
            var rootNode = new FileNode(path);
            rootNode.LoadChildren();
            rootNode.IsExpanded = true;
            RootNodes.Add(rootNode);
        }
        catch
        {
            // Handle errors silently
        }
    }

    [RelayCommand]
    public void ExpandNode(FileNode node)
    {
        if (!node.IsDirectory) return;

        if (!node.IsExpanded)
        {
            node.LoadChildren();
            node.IsExpanded = true;
        }
        else
        {
            node.IsExpanded = false;
        }
    }

    [RelayCommand]
    public void SelectFile(FileNode node)
    {
        if (!node.IsDirectory)
        {
            // File selected - raise event or notify parent to open file
            FileSelected?.Invoke(node.FullPath);
        }
        else
        {
            ExpandNode(node);
        }
    }

    // Event to notify when a file is selected
    public event Action<string>? FileSelected;
}
