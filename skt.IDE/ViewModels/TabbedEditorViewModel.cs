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
using Services;
using skt.IDE;
using skt.IDE.Services.Buss;

public class TabbedEditorViewModel : INotifyPropertyChanged
{
    private DocumentViewModel? _selectedDocument;

    private enum UnsavedChangesResult
    {
        Save,
        DontSave,
        Cancel
    }

    public ObservableCollection<DocumentViewModel> Documents { get; set; } = new();

    public DocumentViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            // If selecting the same document, ensure it stays selected and do nothing else
            if (ReferenceEquals(_selectedDocument, value))
            {
                if (value != null && !value.IsSelected)
                {
                    value.IsSelected = true;
                }
                return;
            }

            var old = _selectedDocument;
            if (SetProperty(ref _selectedDocument, value))
            {
                // Unsubscribe and deselect old after the property actually changed
                if (old != null)
                {
                    old.PropertyChanged -= OnDocumentPropertyChanged;
                    old.IsSelected = false;
                }

                // Subscribe and select new
                if (value != null)
                {
                    value.IsSelected = true;
                    value.PropertyChanged += OnDocumentPropertyChanged;
                }

                OnCommandCanExecuteChanged();
            }
        }
    }

    // Commands
    public RelayCommand NewTabCommand { get; }
    public RelayCommand<DocumentViewModel> CloseTabCommand { get; }
    public RelayCommand<DocumentViewModel> SelectTabCommand { get; }
    public RelayCommand CloseAllTabsCommand { get; }
    public RelayCommand CloseOtherTabsCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }

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

        // Subscribe to global open file requests
        App.EventBus.Subscribe<OpenFileRequestEvent>(OnOpenFileRequest);
    }

    private void OnOpenFileRequest(OpenFileRequestEvent e)
    {
        if (!string.IsNullOrWhiteSpace(e.FilePath))
        {
            _ = OpenFileAsync(e.FilePath);
        }
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
            var result = await ShowUnsavedChangesDialog();
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

        //Before removing document clear its content from the textBox
        document.ClearContent();
        var index = Documents.IndexOf(document);
        Documents.Remove(document);

        if (!string.IsNullOrEmpty(document.FilePath))
        {
            App.EventBus.Publish(new FileClosedEvent(document.FilePath));
        }

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
            var result = await ShowUnsavedChangesDialog();
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

        // Publish close events for all open files
        foreach (var doc in Documents.ToList())
        {
            if (!string.IsNullOrEmpty(doc.FilePath))
            {
                App.EventBus.Publish(new FileClosedEvent(doc.FilePath));
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
            var result = await ShowUnsavedChangesDialog();
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
            if (!string.IsNullOrEmpty(doc.FilePath))
            {
                App.EventBus.Publish(new FileClosedEvent(doc.FilePath));
            }
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

                if (files.Count > 0)
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

                App.EventBus.Publish(new FileOpenedEvent(filePath));
                // Notify status bar about the opened file
                App.EventBus.Publish(new StatusBarMessageEvent($"Opened: {Path.GetFileName(filePath)}", 3000));
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

        // If the document has no path or the file doesn't exist on disk, delegate to Save As
        if (string.IsNullOrEmpty(SelectedDocument.FilePath) || !System.IO.File.Exists(SelectedDocument.FilePath))
        {
            await SaveAsFileAsync();
            return;
        }

        // Save to existing file path and publish update event on success
        var success = await SaveDocumentAsync(SelectedDocument);
        if (success && !string.IsNullOrEmpty(SelectedDocument.FilePath))
        {
            App.EventBus.Publish(new FileUpdatedEvent(SelectedDocument.FilePath));
            // Notify status bar (3 seconds)
            App.EventBus.Publish(new StatusBarMessageEvent("Saved", 3000));
        }
    }

    private async Task SaveAsFileAsync()
    {
        if (SelectedDocument == null) return;

        var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);

        if (topLevel != null)
        {
            string fileType = SelectedDocument.FilePath != null
                ? "*." + Path.GetExtension(SelectedDocument.FilePath).TrimStart('.')
                : "*.txt";

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save File As",
                DefaultExtension = SelectedDocument?.FilePath != null
                    ? Path.GetExtension(SelectedDocument.FilePath).TrimStart('.')
                    : "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("File")
                    {
                        Patterns = new [] {fileType}
                    },
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

            if (file != null && SelectedDocument != null)
            {
                var newFilePath = file.Path.LocalPath;

                SelectedDocument.FilePath = newFilePath;
                var saved = await SaveDocumentAsync(SelectedDocument);
                if (saved)
                {
                    // Always publish FileCreatedEvent for Save As operations (new file)
                    System.Diagnostics.Debug.WriteLine($"Publishing FileCreatedEvent for: {newFilePath}");
                    App.EventBus.Publish(new FileCreatedEvent(newFilePath));
                    // Notify status bar (4 seconds)
                    App.EventBus.Publish(new StatusBarMessageEvent($"Saved As: {System.IO.Path.GetFileName(newFilePath)}", 4000));
                }
            }
        }
    }

    private async Task<bool> SaveDocumentAsync(DocumentViewModel? document)
    {
        if (document?.FilePath == null || string.IsNullOrEmpty(document.FilePath)) return false;

        try
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(document.FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(document.FilePath, document.Content);
            document.IsDirty = false;
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Error Saving File", $"Could not save file: {ex.Message}");
            return false;
        }
    }

    private Task<UnsavedChangesResult> ShowUnsavedChangesDialog()
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
                App.EventBus.Publish(new FileDirtyStateChangedEvent(FilePath ?? "", value));
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

    public void ClearContent()
    {
        _content = string.Empty;
        IsDirty = false;
        OnPropertyChanged(nameof(Content));
    }
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
