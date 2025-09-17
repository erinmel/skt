using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

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

    public string IconPath => IsDirectory
        ? IconMapper.GetFolderIconPath(Name, IsExpanded)
        : IconMapper.GetFileIconPath(Name);

    public FileNode()
    {
    }

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
        OnPropertyChanged(nameof(IconPath));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(IconPath));
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
}
