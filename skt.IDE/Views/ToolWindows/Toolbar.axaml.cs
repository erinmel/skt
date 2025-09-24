using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using skt.IDE.ViewModels;
using skt.IDE.Services.Buss;

namespace skt.IDE.Views.ToolWindows;
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

        // React to project open so the toolbar can enable the New File button
        App.EventBus.Subscribe<ProjectLoadedEvent>(OnProjectLoaded);
        // Subscribe to selected document changes so toolbar can update Save/SaveAs without going through MainWindowViewModel
        App.EventBus.Subscribe<SelectedDocumentChangedEvent>(OnSelectedDocumentChanged);

        // Clean up on unload
        Unloaded += (_, _) =>
        {
            App.EventBus.Unsubscribe<ProjectLoadedEvent>(OnProjectLoaded);
            App.EventBus.Unsubscribe<SelectedDocumentChangedEvent>(OnSelectedDocumentChanged);
        };
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
                App.EventBus.Publish(new StatusBarMessageEvent($"Failed to open project: {e.ErrorMessage}", true));
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
        });
    }

    private void SetTodoStatus(string text)
    {
        App.EventBus.Publish(new StatusBarMessageEvent("TODO (toolbar): " + text, true));
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

    private void NewProjectButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        SetTodoStatus("NewProject");
    }

    private async void OpenProjectButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this) as Window;
            if (top == null)
            {
                // Sent a status bar update
                App.EventBus.Publish(new StatusBarMessageEvent("Problem opening the file explorer.", true));
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
                    App.EventBus.Publish(new ProjectFolderSelectedEvent(folderPath));

                    // Publish a status update for the status bar
                    App.EventBus.Publish(new StatusBarMessageEvent($"Project folder selected: {System.IO.Path.GetFileName(folderPath)}", 3000));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening folder picker: {ex}");
                App.EventBus.Publish(new StatusBarMessageEvent("There was a problem opening the project.", true));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling ProjectFolderSelectedEvent: {ex}");
        }
    }

    private void NewFileButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        // Publish a request for creating a new file; FileExplorerViewModel listens for this event
        App.EventBus.Publish(new CreateFileRequestEvent());
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        App.EventBus.Publish(new SaveFileRequestEvent());
    }

    private void SaveAsButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        App.EventBus.Publish(new SaveAsFilesRequestEvent());
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

}