namespace skt.IDE.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

public class TabbedEditorViewModel : INotifyPropertyChanged
{
    private DocumentViewModel? _selectedDocument;
    private DocumentViewModel? _previousSelectedDocument;

    private enum UnsavedChangesResult
    {
        Save,
        DontSave,
        Cancel
    }

    public ObservableCollection<DocumentViewModel> Documents { get; } = new();

    public DocumentViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            // Unsubscribe from previous document
            if (_previousSelectedDocument != null)
                _previousSelectedDocument.PropertyChanged -= OnDocumentPropertyChanged;

            // Deselect previous document
            if (_selectedDocument != null)
                _selectedDocument.IsSelected = false;

            if (SetProperty(ref _selectedDocument, value))
            {
                if (value != null)
                {
                    value.IsSelected = true;
                    // Subscribe to new document changes
                    value.PropertyChanged += OnDocumentPropertyChanged;
                }

                _previousSelectedDocument = value;
                OnCommandCanExecuteChanged();
            }
        }
    }

    // Commands
    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand CloseAllTabsCommand { get; }
    public ICommand CloseOtherTabsCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }

    public TabbedEditorViewModel()
    {
        NewTabCommand = new RelayCommand(CreateNewTab);
        CloseTabCommand = new RelayCommand<DocumentViewModel>(async doc => await CloseTabAsync(doc), doc => doc != null);
        SelectTabCommand = new RelayCommand<DocumentViewModel>(SelectTab, doc => doc != null);
        CloseAllTabsCommand = new RelayCommand(async () => await CloseAllTabsAsync());
        CloseOtherTabsCommand = new RelayCommand(async () => await CloseOtherTabsAsync(), () => SelectedDocument != null && Documents.Count > 1);
        OpenCommand = new RelayCommand(async () => await OpenFileAsync());
        SaveCommand = new RelayCommand(async () => await SaveFileAsync(), () => SelectedDocument != null);
        SaveAsCommand = new RelayCommand(async () => await SaveAsFileAsync(), () => SelectedDocument != null);
    }

    private void CreateNewTab()
    {
        var doc = new DocumentViewModel();
        Documents.Add(doc);
        SelectedDocument = doc;
    }

    private async Task CloseTabAsync(DocumentViewModel? document)
    {
        if (document == null) return;

        // Check for unsaved changes
        if (document.IsDirty)
        {
            var result = await ShowUnsavedChangesDialog(document.Title);
            switch (result)
            {
                case UnsavedChangesResult.Save:
                    await SaveDocumentAsync(document);
                    break;
                case UnsavedChangesResult.Cancel:
                    return; // Don't close the tab
                case UnsavedChangesResult.DontSave:
                    break; // Continue closing
            }
        }

        var index = Documents.IndexOf(document);
        Documents.Remove(document);

        // Select adjacent tab or create new one if none left
        if (Documents.Count > 0)
        {
            // Select the tab at the same index, or the last tab if index is out of bounds
            var newIndex = Math.Min(index, Documents.Count - 1);
            SelectedDocument = Documents[newIndex];
        }
    }

    private void SelectTab(DocumentViewModel? document)
    {
        if (document != null && Documents.Contains(document))
        {
            SelectedDocument = document;
        }
    }

    private async Task CloseAllTabsAsync()
    {
        var dirtyDocs = Documents.Where(d => d.IsDirty).ToList();

        // Check for unsaved changes in any document
        foreach (var doc in dirtyDocs)
        {
            var result = await ShowUnsavedChangesDialog(doc.Title);
            switch (result)
            {
                case UnsavedChangesResult.Save:
                    await SaveDocumentAsync(doc);
                    break;
                case UnsavedChangesResult.Cancel:
                    return; // Cancel the entire operation
                case UnsavedChangesResult.DontSave:
                    break; // Continue with next document
            }
        }

        Documents.Clear();
        CreateNewTab(); // Always keep at least one tab
    }

    private async Task CloseOtherTabsAsync()
    {
        if (SelectedDocument == null) return;

        var currentDoc = SelectedDocument;
        var otherDocs = Documents.Where(d => d != currentDoc).ToList();
        var dirtyOtherDocs = otherDocs.Where(d => d.IsDirty).ToList();

        // Check for unsaved changes in other documents
        foreach (var doc in dirtyOtherDocs)
        {
            var result = await ShowUnsavedChangesDialog(doc.Title);
            switch (result)
            {
                case UnsavedChangesResult.Save:
                    await SaveDocumentAsync(doc);
                    break;
                case UnsavedChangesResult.Cancel:
                    return; // Cancel the entire operation
                case UnsavedChangesResult.DontSave:
                    break; // Continue with next document
            }
        }

        // Remove all other documents
        foreach (var doc in otherDocs)
        {
            Documents.Remove(doc);
        }

        // Ensure current document is still selected
        SelectedDocument = currentDoc;
    }

    public async Task OpenFileAsync(string? filePath = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            // Show file dialog
            var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);

            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Open File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Text Files")
                        {
                            Patterns = new[] { "*.txt", "*.md", "*.cs", "*.xaml", "*.json", "*.xml" }
                        },
                        new FilePickerFileType("All Files")
                        {
                            Patterns = new[] { "*.*" }
                        }
                    }
                });

                if (files?.Count > 0)
                {
                    filePath = files[0].Path.LocalPath;
                }
            }
        }

        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath);

                // Check if file is already open
                var existingDoc = Documents.FirstOrDefault(d =>
                    string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

                if (existingDoc != null)
                {
                    SelectedDocument = existingDoc;
                    return;
                }

                var doc = new DocumentViewModel
                {
                    FilePath = filePath
                };
                doc.SetContentFromFile(content);
                Documents.Add(doc);
                SelectedDocument = doc;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Error Opening File", $"Could not open file: {ex.Message}");
            }
        }
    }

    public async Task SaveAsync()
    {
        await SaveFileAsync();
    }

    public async Task SaveAsAsync()
    {
        await SaveAsFileAsync();
    }

    private async Task SaveFileAsync()
    {
        if (SelectedDocument == null) return;

        if (string.IsNullOrEmpty(SelectedDocument.FilePath))
        {
            await SaveAsFileAsync();
            return;
        }

        await SaveDocumentAsync(SelectedDocument);
    }

    private async Task SaveAsFileAsync()
    {
        if (SelectedDocument == null) return;

        var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);

        if (topLevel != null)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save File As",
                DefaultExtension = "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Text Files")
                    {
                        Patterns = new[] { "*.txt" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (file != null)
            {
                SelectedDocument.FilePath = file.Path.LocalPath;
                await SaveDocumentAsync(SelectedDocument);
            }
        }
    }

    private async Task SaveDocumentAsync(DocumentViewModel document)
    {
        if (document?.FilePath == null || string.IsNullOrEmpty(document.FilePath)) return;

        try
        {
            await File.WriteAllTextAsync(document.FilePath, document.Content);
            document.IsDirty = false;
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Error Saving File", $"Could not open file: {ex.Message}");
        }
    }

    private Task<UnsavedChangesResult> ShowUnsavedChangesDialog(string fileName)
    {
        // For now, return DontSave - in a real app, show a dialog
        // You can implement this with Avalonia's MessageBox or custom dialog
        return Task.FromResult(UnsavedChangesResult.DontSave);
    }

    private Task ShowErrorDialog(string title, string message)
    {
        // For now, just write to debug - in a real app, show a dialog
        System.Diagnostics.Debug.WriteLine($"{title}: {message}");
        return Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnCommandCanExecuteChanged()
    {
        // Trigger CanExecuteChanged for all commands that depend on document state
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveAsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CloseOtherTabsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.IsDirty))
        {
            OnCommandCanExecuteChanged();
        }
    }
}

public class DocumentViewModel : INotifyPropertyChanged
{
    private string _title = "Untitled";
    private string _content = "";
    private string? _filePath;
    private bool _isDirty;
    private bool _isSelected;

    public string Title
    {
        get => _isDirty ? $"{_title}*" : _title;
        set => SetProperty(ref _title, value);
    }

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
            {
                IsDirty = true;
            }
        }
    }

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                Title = string.IsNullOrEmpty(value) ? "Untitled" : Path.GetFileName(value);
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected internal void SetContentFromFile(string content)
    {
        _content = content;
        IsDirty = false;
        OnPropertyChanged(nameof(Content));
    }

    public bool isDirty() => _isDirty;
    public bool isSelected() => _isSelected;
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    public void Execute(object? parameter) => _execute((T?)parameter);

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
