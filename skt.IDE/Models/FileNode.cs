using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace skt.IDE.Models;

public class FileNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public ObservableCollection<FileNode> Children { get; set; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();

                // Load children when expanded for the first time
                if (value && IsDirectory && Children.Count == 0)
                {
                    LoadChildren();
                }
            }
        }
    }

    public string DisplayName => Name;
    public string Icon => IsDirectory ? "📁" : "📄";

    public event PropertyChangedEventHandler? PropertyChanged;

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
            Name = Path.GetPathRoot(path) ?? path;
        }
    }

    public void LoadChildren()
    {
        if (!IsDirectory) return;

        Children.Clear();

        try
        {
            // Add directories first
            foreach (var dir in Directory.GetDirectories(FullPath))
            {
                Children.Add(new FileNode(dir));
            }

            // Add files
            foreach (var file in Directory.GetFiles(FullPath))
            {
                Children.Add(new FileNode(file));
            }
        }
        catch
        {
            // Handle access denied or other exceptions silently
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
