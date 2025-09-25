using System.Collections.Generic;
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

    [ObservableProperty]
    private bool _isEditing; // inline rename state

    private string? _originalNameBackup;
    private bool _childrenLoaded; // tracks if immediate children have been loaded
    public bool IsPlaceholder { get; private set; }

    private FileNode(bool placeholder)
    {
        IsPlaceholder = placeholder;
        _name = "";
        _fullPath = string.Empty;
    }

    public static FileNode CreatePlaceholder() => new(true);

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
            Children.Add(CreatePlaceholder());
        }
    }

    private void EnsureChildrenLoaded()
    {
        if (!IsDirectory || _childrenLoaded) return;
        LoadChildren();
    }

    private void ReloadChildrenIfLoaded()
    {
        if (!IsDirectory) return;
        if (_childrenLoaded)
        {
            _childrenLoaded = false;
            LoadChildren();
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value) EnsureChildrenLoaded();
        OnPropertyChanged(nameof(IconKey));
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(IconKey));

    public void BeginInlineEdit()
    {
        if (IsEditing) return;
        _originalNameBackup = Name;
        IsEditing = true;
    }

    public void CancelInlineEdit()
    {
        if (!IsEditing) return;
        if (_originalNameBackup != null) Name = _originalNameBackup;
        _originalNameBackup = null;
        IsEditing = false;
    }

    public void CompleteInlineEdit()
    {
        if (!IsEditing) return;
        _originalNameBackup = null;
        IsEditing = false;
    }

    private void LoadChildren()
    {
        if (!IsDirectory) return;
        if (Children.Count == 1 && Children[0].IsPlaceholder) Children.Clear();
        try
        {
            foreach (var dir in Directory.GetDirectories(FullPath).OrderBy(p => p))
            {
                Children.Add(new FileNode(dir));
            }
            foreach (var file in Directory.GetFiles(FullPath).OrderBy(p => p))
            {
                Children.Add(new FileNode(file));
            }
            if (Children.Count == 0)
            {
                // add placeholder back so we can attempt later if FS latency caused empty
                Children.Add(CreatePlaceholder());
                _childrenLoaded = false; // allow retry
                return;
            }
        }
        catch
        {
            if (Children.Count == 0) Children.Add(CreatePlaceholder());
            _childrenLoaded = false;
            return;
        }
        _childrenLoaded = true;
    }

    public void RefreshChildren()
    {
        if (!IsDirectory) return;
        var expandedDirectories = Children
            .Where(c => c.IsDirectory && c.IsExpanded)
            .Select(c => c.Name)
            .ToHashSet();
        ReloadChildrenIfLoaded();
        foreach (var child in Children.Where(c => c.IsDirectory))
        {
            if (expandedDirectories.Contains(child.Name)) child.IsExpanded = true;
        }
    }

    public void MergeChildrenWithFileSystem()
    {
        if (!IsDirectory) return;
        EnsureChildrenLoaded();
        if (!_childrenLoaded) return; // avoid expensive merge until we have real data

        List<string> dirPaths = new();
        List<string> filePaths = new();
        try
        {
            dirPaths = Directory.GetDirectories(FullPath).OrderBy(p => p).ToList();
            filePaths = Directory.GetFiles(FullPath).OrderBy(p => p).ToList();
        }
        catch { }

        var expectedPaths = new List<string>(dirPaths.Count + filePaths.Count);
        expectedPaths.AddRange(dirPaths);
        expectedPaths.AddRange(filePaths);

        var existingByPath = Children.Where(c => !c.IsPlaceholder)
            .ToDictionary(c => c.FullPath, c => c, System.StringComparer.OrdinalIgnoreCase);
        var newChildren = new ObservableCollection<FileNode>();

        foreach (var path in expectedPaths)
        {
            if (existingByPath.TryGetValue(path, out var existing))
            {
                newChildren.Add(existing);
                if (existing.IsDirectory && existing.IsExpanded) existing.MergeChildrenWithFileSystem();
            }
            else
            {
                newChildren.Add(new FileNode(path));
            }
        }
        Children.Clear();
        foreach (var child in newChildren) Children.Add(child);
        _childrenLoaded = true;
        if (Children.Count == 0) Children.Add(CreatePlaceholder());
    }

    public FileNode? FindNodeByPath(string path)
    {
        if (string.Equals(FullPath, path, System.StringComparison.OrdinalIgnoreCase)) return this;
        EnsureChildrenLoaded();
        foreach (var child in Children.Where(c => !c.IsPlaceholder))
        {
            var found = child.FindNodeByPath(path);
            if (found != null) return found;
        }
        return null;
    }
}
