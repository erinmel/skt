using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace skt.IDE.Views.Dialogs;

public sealed class ProjectCreationResult
{
    public string BasePath { get; }
    public string ProjectName { get; }
    public string FinalPath { get; }
    public ProjectCreationResult(string basePath, string projectName, string finalPath)
    {
        BasePath = basePath;
        ProjectName = projectName;
        FinalPath = finalPath;
    }
}

public partial class ProjectCreationDialog : Window
{
    private TextBox? _basePathTextBox;
    private TextBox? _projectNameTextBox;
    private TextBlock? _finalPathText;
    private TextBlock? _warningText;
    private Button? _browseButton;
    private Button? _createButton;
    private Button? _cancelButton;

    private string BasePath => _basePathTextBox?.Text?.Trim() ?? string.Empty;
    private string ProjectName => _projectNameTextBox?.Text?.Trim() ?? string.Empty;
    private string FinalPath => Path.Combine(BasePath, ProjectName);

    public ProjectCreationDialog()
    {
        InitializeComponent();
        AttachControls();
        SetupDefaults();
        HookEvents();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void AttachControls()
    {
        _basePathTextBox = this.FindControl<TextBox>("BasePathTextBox");
        _projectNameTextBox = this.FindControl<TextBox>("ProjectNameTextBox");
        _finalPathText = this.FindControl<TextBlock>("FinalPathText");
        _warningText = this.FindControl<TextBlock>("WarningText");
        _browseButton = this.FindControl<Button>("BrowseButton");
        _createButton = this.FindControl<Button>("CreateButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
    }

    private void SetupDefaults()
    {
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs) || !Directory.Exists(docs))
            {
                docs = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            var defaultBase = Path.Combine(docs, "SktProjects");
            if (!Directory.Exists(defaultBase))
            {
                try { Directory.CreateDirectory(defaultBase); } catch { /* ignore */ }
            }
            if (_basePathTextBox != null) _basePathTextBox.Text = defaultBase;
            if (_projectNameTextBox != null) _projectNameTextBox.Text = "NewProject";
            UpdateFinalPathAndValidation();
        }
        catch
        {
            // ignore
        }
    }

    private void HookEvents()
    {
        if (_projectNameTextBox != null)
        {
            _projectNameTextBox.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty) UpdateFinalPathAndValidation();
            };
        }
        if (_basePathTextBox != null)
        {
            _basePathTextBox.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty) UpdateFinalPathAndValidation();
            };
        }
        if (_browseButton != null) _browseButton.Click += async (_, _) => await BrowseForBasePathAsync();
        if (_createButton != null) _createButton.Click += (_, _) => TryCloseWithResult();
        if (_cancelButton != null) _cancelButton.Click += (_, _) => Close(null);

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close(null);
            else if (e.Key == Key.Enter) TryCloseWithResult();
        };

        Opened += (_, _) => _projectNameTextBox?.Focus();
    }

    private void UpdateFinalPathAndValidation()
    {
        if (_finalPathText != null) _finalPathText.Text = string.IsNullOrWhiteSpace(ProjectName) ? string.Empty : FinalPath;

        var invalid = ContainsInvalidNameChars(ProjectName);
        var exists = Directory.Exists(FinalPath);
        var missingBase = !string.IsNullOrWhiteSpace(BasePath) && !Directory.Exists(BasePath);

        string warning = string.Empty;
        if (string.IsNullOrWhiteSpace(ProjectName)) warning = "Project name is required";
        else if (invalid) warning = "Project name contains invalid characters";
        else if (missingBase) warning = "Base path does not exist";
        else if (exists) warning = "A project with this name already exists";

        if (_warningText != null) _warningText.Text = warning;
        if (_createButton != null) _createButton.IsEnabled = string.IsNullOrEmpty(warning);
    }

    private static bool ContainsInvalidNameChars(string name)
        => !string.IsNullOrEmpty(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

    private async Task BrowseForBasePathAsync()
    {
        try
        {
            var top = this as Window;
            var result = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Base Folder",
                AllowMultiple = false
            });
            if (result.Count > 0)
            {
                _basePathTextBox!.Text = result[0].Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void TryCloseWithResult()
    {
        UpdateFinalPathAndValidation();
        if (_createButton != null && !_createButton.IsEnabled) return;
        if (string.IsNullOrWhiteSpace(BasePath) || string.IsNullOrWhiteSpace(ProjectName)) return;
        Close(new ProjectCreationResult(BasePath, ProjectName, FinalPath));
    }

    public Task<ProjectCreationResult?> ShowAsync(Window owner) => ShowDialog<ProjectCreationResult?>(owner);
}
