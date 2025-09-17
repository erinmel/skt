using System.Collections.ObjectModel;
using System.IO;

namespace skt.IDE.Models;

public class FileNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public ObservableCollection<FileNode> Children { get; set; } = new();
    public bool IsExpanded { get; set; }

    public string DisplayName => Name;
    public string Icon => IsDirectory ? "📁" : "📄";

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

        // Load children immediately for simplicity
        if (IsDirectory)
        {
            LoadChildren();
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
}
