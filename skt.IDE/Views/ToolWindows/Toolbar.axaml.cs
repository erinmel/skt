using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using skt.IDE.ViewModels;
using skt.IDE.Services;

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
    }

    private void SetTodoStatus(string text)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StatusMessage = "TODO (toolbar -> main): " + text;
        }
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

                    // Update the status via DataContext if available
                    if (DataContext is MainWindowViewModel vm)
                    {
                        vm.StatusMessage = $"Project folder selected: {System.IO.Path.GetFileName(folderPath)}";
                    }
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
        SetTodoStatus("NewFile");
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        SetTodoStatus("Save");
    }

    private void SaveAsButton_Click(object? sender, RoutedEventArgs e)
    {
        SetTodoStatus("SaveAs");
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