using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace skt.IDE.Models;

public partial class FileNode : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private ObservableCollection<FileNode> _children = new();

    [ObservableProperty]
    private bool _isExpanded;

    public string DisplayName => Name;

    public string IconKey => IsDirectory
        ? IconMapper.GetFolderIconKey(Name, IsExpanded)
        : IconMapper.GetFileIconKey(Name);

    public FileNode(string path)
    {
        FullPath = path;
        Name = Path.GetFileName(path);
        IsDirectory = Directory.Exists(path);

        if (IsDirectory && string.IsNullOrEmpty(Name))
        {
            Name = Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (IsDirectory)
        {
            LoadChildren();
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IconKey));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(IconKey));
    }

    private void LoadChildren()
    {
        if (!IsDirectory) return;

        Children.Clear();

        try
        {
            foreach (var dir in Directory.GetDirectories(FullPath))
            {
                Children.Add(new FileNode(dir));
            }

            foreach (var file in Directory.GetFiles(FullPath))
            {
                Children.Add(new FileNode(file));
            }
        }
        catch
        {
            // ignore IO errors
        }
    }

    public void RefreshChildren()
    {
        if (!IsDirectory) return;

        var expandedDirectories = Children
            .Where(c => c.IsDirectory && c.IsExpanded)
            .Select(c => c.Name)
            .ToHashSet();

        LoadChildren();

        foreach (var child in Children.Where(c => c.IsDirectory))
        {
            if (expandedDirectories.Contains(child.Name))
            {
                child.IsExpanded = true;
            }
        }
    }
}
