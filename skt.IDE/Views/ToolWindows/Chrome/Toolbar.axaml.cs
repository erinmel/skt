using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using skt.IDE.ViewModels;
using skt.IDE.Services.Buss;
using System.IO;
using System.Threading.Tasks;
using skt.IDE.Views.Dialogs;
using skt.IDE.Views.Shell;
using CommunityToolkit.Mvvm.Messaging;

namespace skt.IDE.Views.ToolWindows.Chrome;
public partial class Toolbar : UserControl
{
    public Toolbar()
    {
        InitializeComponent();

        var drag = this.FindControl<Border>("DragArea");
        if (drag != null)
        {
            drag.PointerPressed += DragArea_PointerPressed;
            drag.DoubleTapped += DragArea_DoubleTapped;
        }

        var logo = this.FindControl<StackPanel>("LogoPanel");
        if (logo != null)
        {
            logo.PointerPressed += DragArea_PointerPressed;
            logo.DoubleTapped += DragArea_DoubleTapped;
        }

        // Ensure New File is disabled until a project is successfully loaded
        var newBtn = this.FindControl<Button>("NewFileButton");
        if (newBtn != null)
            newBtn.IsEnabled = false;

        // Ensure Save/SaveAs start disabled; toolbar will enable/disable based on selected document events
        var saveBtn = this.FindControl<Button>("SaveButton");
        if (saveBtn != null)
            saveBtn.IsEnabled = false;
        var saveAsBtn = this.FindControl<Button>("SaveAsButton");
        if (saveAsBtn != null)
            saveAsBtn.IsEnabled = false;
        var lexMenuItem = this.FindControl<MenuItem>("LexicalAnalysisMenuItem");
        if (lexMenuItem != null)
            lexMenuItem.IsEnabled = false;
        var syntaxMenuItem = this.FindControl<MenuItem>("SyntacticAnalysisMenuItem");
        if (syntaxMenuItem != null)
            syntaxMenuItem.IsEnabled = false;

        var semanticMenuItem = this.FindControl<MenuItem>("SemanticAnalysisMenuItem");
        if (semanticMenuItem != null)
            semanticMenuItem.IsEnabled = false;

        // React to project open so the toolbar can enable the New File button
        App.Messenger.Register<ProjectLoadedEvent>(this, (r, m) => OnProjectLoaded(m));
        // Subscribe to selected document changes so toolbar can update Save/SaveAs without going through MainWindowViewModel
        App.Messenger.Register<SelectedDocumentChangedEvent>(this, (r, m) => OnSelectedDocumentChanged(m));
        // Subscribe to lexical analysis completion to enable syntax analysis
        App.Messenger.Register<LexicalAnalysisCompletedEvent>(this, (r, m) => OnLexicalAnalysisCompleted(m));
        // Subscribe to syntax analysis completion to enable semantic analysis
        App.Messenger.Register<SyntaxAnalysisCompletedEvent>(this, (r, m) => OnSyntaxAnalysisCompleted(m));

        // Clean up on unload
        Unloaded += (_, _) => App.Messenger.UnregisterAll(this);
    }

    private void OnProjectLoaded(ProjectLoadedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var btn = this.FindControl<Button>("NewFileButton");
            if (btn != null)
                btn.IsEnabled = e.Success;

            if (!e.Success && DataContext is MainWindowViewModel)
            {
                App.Messenger.Send(new StatusBarMessageEvent($"Failed to open project: {e.ErrorMessage}", true));
            }
        });
    }

    private void OnSelectedDocumentChanged(SelectedDocumentChangedEvent e)
    {
        // Update UI elements on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            var saveBtn = this.FindControl<Button>("SaveButton");
            if (saveBtn != null)
                saveBtn.IsEnabled = e is { HasSelection: true, IsDirty: true };

            var saveAsBtn = this.FindControl<Button>("SaveAsButton");
            if (saveAsBtn != null)
                saveAsBtn.IsEnabled = e.HasSelection;

            var lexMenuItem = this.FindControl<MenuItem>("LexicalAnalysisMenuItem");
            if (lexMenuItem != null)
                lexMenuItem.IsEnabled = e.HasSelection && !string.IsNullOrEmpty(e.FilePath);

            var syntaxMenuItem = this.FindControl<MenuItem>("SyntacticAnalysisMenuItem");
            if (syntaxMenuItem != null)
                syntaxMenuItem.IsEnabled = e.HasSelection && !string.IsNullOrEmpty(e.FilePath);
        });
    }

    private void OnLexicalAnalysisCompleted(LexicalAnalysisCompletedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var syntaxMenuItem = this.FindControl<MenuItem>("SyntacticAnalysisMenuItem");
            if (syntaxMenuItem != null)
                syntaxMenuItem.IsEnabled = e.ErrorCount == 0 && !string.IsNullOrEmpty(e.FilePath);
        });
    }

    private void OnSyntaxAnalysisCompleted(SyntaxAnalysisCompletedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var semanticMenuItem = this.FindControl<MenuItem>("SemanticAnalysisMenuItem");
            if (semanticMenuItem != null)
                semanticMenuItem.IsEnabled = e.Errors.Count == 0 && !string.IsNullOrEmpty(e.FilePath);
        });
    }

    private void SetTodoStatus(string text)
    {
        App.Messenger.Send(new StatusBarMessageEvent("TODO (toolbar): " + text, true));
    }

    private void DragArea_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (TopLevel.GetTopLevel(this) is not MainWindow top) return;
            top.BeginMoveDrag(e);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during window drag: {ex}");
        }
    }

    private void DragArea_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow top)
        {
            top.WindowState = top.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    private async void NewProjectButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        try
        {
            await CreateNewProjectAsync();
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling NewProjectButton_Click: {e}");
        }
    }

    private async Task CreateNewProjectAsync()
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                App.Messenger.Send(new StatusBarMessageEvent("Cannot open project dialog.", true));
                return;
            }

            var dialog = new ProjectCreationDialog();
            var result = await dialog.ShowAsync(owner);
            if (result == null) return;

            var basePath = result.BasePath;
            var projectName = result.ProjectName;
            var finalPath = result.FinalPath;

            try
            {
                if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
                if (Directory.Exists(finalPath))
                {
                    App.Messenger.Send(new StatusBarMessageEvent("Project already exists.", true));
                    return;
                }
                Directory.CreateDirectory(finalPath);
                var mainFilePath = Path.Combine(finalPath, "main.skt");
                var template = "main {\n\n}\n";
                await File.WriteAllTextAsync(mainFilePath, template);
                App.Messenger.Send(new FileCreatedEvent(mainFilePath));

                // Open project
                App.Messenger.Send(new ProjectFolderSelectedEvent(finalPath));
                // Open main file
                App.Messenger.Send(new OpenFileRequestEvent(mainFilePath));
                // Set caret after file open (schedule slight delay)
                Dispatcher.UIThread.Post(() =>
                {
                    var caretIndex = template.IndexOf("<- carete here", StringComparison.Ordinal);
                    if (caretIndex >= 0)
                    {
                        App.Messenger.Send(new SetCaretPositionRequestEvent(mainFilePath, caretIndex));
                    }
                }, DispatcherPriority.Background);

                App.Messenger.Send(new StatusBarMessageEvent($"Created project: {projectName}", 3000));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating project: {ex}");
                App.Messenger.Send(new StatusBarMessageEvent("Failed to create project.", true));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CreateNewProjectAsync: {ex}");
        }
    }

    private async void OpenProjectButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this) as Window;
            if (top == null)
            {
                App.Messenger.Send(new StatusBarMessageEvent("Problem opening the file explorer.", true));
                return;
            }

            try
            {
                var result = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Project Folder",
                    AllowMultiple = false
                });

                if (result.Count > 0)
                {
                    var folder = result[0];
                    var folderPath = folder.Path.LocalPath;

                    // Publish the selected folder path on the global EventBus
                    App.Messenger.Send(new ProjectFolderSelectedEvent(folderPath));

                    // Publish a status update for the status bar
                    App.Messenger.Send(new StatusBarMessageEvent($"Project folder selected: {Path.GetFileName(folderPath)}", 3000));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening folder picker: {ex}");
                App.Messenger.Send(new StatusBarMessageEvent("There was a problem opening the project.", true));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling ProjectFolderSelectedEvent: {ex}");
        }
    }

    private void NewFileButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        App.Messenger.Send(new CreateFileRequestEvent());
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        App.Messenger.Send(new SaveFileRequestEvent());
    }

    private void SaveAsButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        App.Messenger.Send(new SaveAsFilesRequestEvent());
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        SetTodoStatus("Settings");
    }

    private void Minimize_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow top)
        {
            top.WindowState = WindowState.Minimized;
        }
    }

    private void Restore_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow top)
        {
            top.WindowState = top.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        var top = TopLevel.GetTopLevel(this) as MainWindow;
        top?.Close();
    }

    private void LexicalAnalysisMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            App.Messenger.Send(new StatusBarMessageEvent("No context for lexical analysis", true));
            return;
        }
        var path = vm.TabbedEditorViewModel.SelectedDocument?.FilePath;
        if (string.IsNullOrEmpty(path))
        {
            App.Messenger.Send(new StatusBarMessageEvent("No file selected to tokenize", true));
            return;
        }
        App.Messenger.Send(new TokenizeFileRequestEvent(path, writeTokenFile: false));
        App.Messenger.Send(new ShowToolWindowRequestEvent("TokensToggle"));
        App.Messenger.Send(new StatusBarMessageEvent("Lexical analysis started (file)", 2000));
    }

    private void SyntacticAnalysisMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            App.Messenger.Send(new StatusBarMessageEvent("No context for syntax analysis", true));
            return;
        }
        var path = vm.TabbedEditorViewModel.SelectedDocument?.FilePath;
        if (string.IsNullOrEmpty(path))
        {
            App.Messenger.Send(new StatusBarMessageEvent("No file selected to parse", true));
            return;
        }

        // Call syntax analysis directly - it will cascade
        App.Messenger.Send(new ParseFileRequestEvent(path));
        App.Messenger.Send(new ShowToolWindowRequestEvent("SyntaxTreeToggle"));
        App.Messenger.Send(new StatusBarMessageEvent("Running syntax analysis", 2000));
    }

    private void SemanticAnalysisMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        App.Messenger.Send(new ShowToolWindowRequestEvent("SemanticTreeToggle"));
        App.Messenger.Send(new StatusBarMessageEvent("Semantic analysis running live", 2000));
    }
}
