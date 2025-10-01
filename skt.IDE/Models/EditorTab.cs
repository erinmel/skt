using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace skt.IDE.Models;

public partial class EditorTab : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string _content = "";
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _isSelected;

    public EditorTab()
    {
    }

    public EditorTab(string filePath, string content)
    {
        FilePath = filePath;
        Content = content;
        Title = string.IsNullOrEmpty(filePath) ? "Untitled" : Path.GetFileName(filePath);
        IsDirty = false;
    }

    public string DisplayTitle => IsDirty ? $"{Title}*" : Title;

    partial void OnContentChanged(string value)
    {
        IsDirty = true;
        OnPropertyChanged(nameof(DisplayTitle));
    }

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }

    public void MarkAsSaved()
    {
        IsDirty = false;
    }
}
