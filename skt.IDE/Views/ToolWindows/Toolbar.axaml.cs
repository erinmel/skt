using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using skt.IDE.ViewModels;
using skt.IDE.Services;
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

        // React to project open so the toolbar can enable the New File button
        App.EventBus.Subscribe<ProjectLoadedEvent>(OnProjectLoaded);
        // Clean up on unload
        Unloaded += (_, __) => App.EventBus.Unsubscribe<ProjectLoadedEvent>(OnProjectLoaded);
    }

    private void OnProjectLoaded(ProjectLoadedEvent e)
    {
        // Ensure UI updates happen on the UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var btn = this.FindControl<Button>("NewFileButton");
            if (btn != null)
                btn.IsEnabled = e.Success;

            if (!e.Success && DataContext is MainWindowViewModel vm)
            {
                App.EventBus.Publish(new StatusBarMessageEvent($"Failed to open project: {e.ErrorMessage}", 5000));
            }
        });
    }

    private void SetTodoStatus(string text)
    {
        // Prefer event-based status updates so the status bar UI is the single source of truth
        App.EventBus.Publish(new StatusBarMessageEvent("TODO (toolbar -> main): " + text, 3000));
    }

    private void DragArea_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var top = TopLevel.GetTopLevel(this) as skt.IDE.Views.MainWindow;
            if (top != null)
            {
                try { top.BeginMoveDrag(e); } catch { }
            }
        }
    }

    private void DragArea_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this) as skt.IDE.Views.MainWindow;
        if (top != null)
        {
            top.WindowState = top.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    private void NewProjectButton_Click(object? sender, RoutedEventArgs e)
    {
        SetTodoStatus("NewProject");
    }

    private async void OpenProjectButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this) as Window;
            if (top == null)
            {
                SetTodoStatus("OpenProject: unable to get window storage provider");
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
            catch (System.Exception ex)
            {
                SetTodoStatus($"OpenProject error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling ProjectFolderSelectedEvent: {ex}");
        }
    }

    private void NewFileButton_Click(object? sender, RoutedEventArgs e)
    {
        // Publish a request for creating a new file; FileExplorerViewModel listens for this event
        App.EventBus.Publish(new CreateFileRequestEvent());
        App.EventBus.Publish(new StatusBarMessageEvent("New file requested", 2000));
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var editor = vm.TabbedEditorViewModel;
            if (editor != null)
            {
                await editor.SaveAsync();

                // If the selected document is not dirty after the save, assume success
                if (editor.SelectedDocument != null && !editor.SelectedDocument.IsDirty)
                    App.EventBus.Publish(new StatusBarMessageEvent("Saved", 3000));
                else
                    App.EventBus.Publish(new StatusBarMessageEvent("Save canceled or failed", 3000));
            }
        }
        else
        {
            SetTodoStatus("Save");
        }
    }

    private async void SaveAsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var editor = vm.TabbedEditorViewModel;
            if (editor != null)
            {
                await editor.SaveAsAsync();

                if (editor.SelectedDocument != null && !editor.SelectedDocument.IsDirty)
                    App.EventBus.Publish(new StatusBarMessageEvent("Saved As", 4000));
                else
                    App.EventBus.Publish(new StatusBarMessageEvent("Save As canceled or failed", 3000));
            }
        }
        else
        {
            SetTodoStatus("SaveAs");
        }
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        SetTodoStatus("Settings");
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this) as skt.IDE.Views.MainWindow;
        if (top != null)
        {
            top.WindowState = WindowState.Minimized;
        }
    }

    private void Restore_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this) as skt.IDE.Views.MainWindow;
        if (top != null)
        {
            top.WindowState = top.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this) as skt.IDE.Views.MainWindow;
        top?.Close();
    }

}