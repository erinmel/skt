using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Models;
using Avalonia.Threading;
using skt.IDE.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using skt.IDE.Views.Dialogs;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class FileExplorerViewModel : ViewModelBase
{
    private const String NoProjectName = "Open A Project to Begin";
    
    [ObservableProperty]
    private ObservableCollection<FileNode> _rootNodes = new();

    [ObservableProperty]
    private string _projectName = NoProjectName;

    private string _currentProjectPath = string.Empty;

    public RelayCommand<FileNode> AddNewFileCommand { get; }
    public RelayCommand<FileNode> AddNewFolderCommand { get; }
    public RelayCommand<FileNode> RenameResourceCommand { get; }
    public RelayCommand<FileNode> CopyCommand { get; }
    public RelayCommand<FileNode> CutCommand { get; }
    public RelayCommand<FileNode> PasteCommand { get; }
    public RelayCommand<FileNode> DeleteCommand { get; }

    // In-memory clipboard
    private readonly List<string> _clipboardPaths = new();
    private bool _isCutOperation;

    public FileExplorerViewModel()
    {
        ProjectName = NoProjectName;
        App.EventBus.Subscribe<FileCreatedEvent>(OnFileCreated);
        App.EventBus.Subscribe<FileUpdatedEvent>(OnFileUpdated);
        App.EventBus.Subscribe<CreateFileRequestEvent>(_ => AddNewFile(null));
        App.EventBus.Subscribe<ProjectFolderSelectedEvent>(OnProjectFolderSelected);
        AddNewFileCommand = new RelayCommand<FileNode>(AddNewFile);
        AddNewFolderCommand = new RelayCommand<FileNode>(AddNewFolder);
        RenameResourceCommand = new RelayCommand<FileNode>(RenameResource);
        CopyCommand = new RelayCommand<FileNode>(Copy);
        CutCommand = new RelayCommand<FileNode>(Cut);
        PasteCommand = new RelayCommand<FileNode>(Paste);
        DeleteCommand = new RelayCommand<FileNode>(Delete);
    }

    private async void OnProjectFolderSelected(ProjectFolderSelectedEvent e)
    {
        try
        {
            if (string.IsNullOrEmpty(e.FolderPath) || !Directory.Exists(e.FolderPath))
            {
                System.Diagnostics.Debug.WriteLine($"ProjectFolderSelectedEvent: invalid path '{e.FolderPath}'");
                return;
            }

            // Ensure loading happens on the UI thread
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await LoadProject(e.FolderPath);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling ProjectFolderSelectedEvent: {ex}");
        }
    }

    private async void OnFileCreated(FileCreatedEvent fileEvent)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"FileExplorerViewModel received FileCreatedEvent for: {fileEvent.FilePath}");
            System.Diagnostics.Debug.WriteLine($"Current project path: {_currentProjectPath}");

            if (string.IsNullOrEmpty(_currentProjectPath) || !fileEvent.FilePath.StartsWith(_currentProjectPath))
            {
                System.Diagnostics.Debug.WriteLine("File is not in current project - ignoring event");
                return;
            }

            System.Diagnostics.Debug.WriteLine("Refreshing file tree...");
            // Use a simple approach: just refresh the entire file tree
            // This is more reliable than trying to navigate the hierarchy
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await LoadProject(_currentProjectPath);
            });
            System.Diagnostics.Debug.WriteLine("File tree refresh completed");
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling FileCreatedEvent: {e}");
        }
    }

    private async void OnFileUpdated(FileUpdatedEvent fileEvent)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"FileExplorerViewModel received FileUpdatedEvent for: {fileEvent.FilePath}");
            System.Diagnostics.Debug.WriteLine($"Current project path: {_currentProjectPath}");

            if (string.IsNullOrEmpty(_currentProjectPath) || !fileEvent.FilePath.StartsWith(_currentProjectPath))
            {
                System.Diagnostics.Debug.WriteLine("File is not in current project - ignoring event");
                return;
            }

            System.Diagnostics.Debug.WriteLine("Refreshing file tree due to file update...");
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await LoadProject(_currentProjectPath);
            });
            System.Diagnostics.Debug.WriteLine("File tree refresh completed after update");
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling FileCreatedEvent: {e}");
        }
    }

    public async Task LoadProject(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
        {
            ProjectName = NoProjectName;
            _currentProjectPath = string.Empty;
            return;
        }

        _currentProjectPath = projectPath;
        List<FileNode> childNodes = new();
        string projectName = Path.GetFileName(projectPath);

        // Ensure we have a valid project name
        if (string.IsNullOrEmpty(projectName))
        {
            projectName = Path.GetFileName(Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        // Build the file tree off the UI thread
        await Task.Run(() =>
        {
            try
            {
                // Load directories first
                foreach (var dir in Directory.GetDirectories(projectPath))
                {
                    childNodes.Add(new FileNode(dir));
                }

                // Load files
                foreach (var file in Directory.GetFiles(projectPath))
                {
                    childNodes.Add(new FileNode(file));
                }
            }
            catch
            {
                // ignore IO errors
            }
        });

        // Apply changes on the UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RootNodes.Clear();
            foreach (var node in childNodes)
            {
                RootNodes.Add(node);
            }
            ProjectName = string.IsNullOrEmpty(projectName) ? NoProjectName : projectName;
        });

        App.EventBus.Publish(new ProjectLoadedEvent(projectPath));
    }

    private async Task RefreshFileTree()
    {
        if (!string.IsNullOrEmpty(_currentProjectPath))
        {
            await LoadProject(_currentProjectPath);
        }
    }

    // Event to notify when a file is selected
    public event Action<string>? FileSelected;

    internal void NotifyFileSelected(string filePath)
    {
        FileSelected?.Invoke(filePath);
    }

    private async void AddNewFile(FileNode? node)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentProjectPath)) return;

            var targetDir = GetTargetDirectory(node);
            try
            {
                Directory.CreateDirectory(targetDir);
                var createdPath = GetUniqueFilePath(targetDir, "New File", ".skt");
                await Task.Run(() => File.WriteAllText(createdPath, string.Empty));
                App.EventBus.Publish(new FileCreatedEvent(createdPath));

                // Prompt user to rename the newly created file
                var currentExt = Path.GetExtension(createdPath);
                var defaultNameNoExt = Path.GetFileNameWithoutExtension(createdPath);
                var input = await PromptForTextAsync("Rename File", "Enter new file name (extension optional):", defaultNameNoExt);
                if (!string.IsNullOrWhiteSpace(input))
                {
                    input = input.Trim();
                    var newName = input.Contains('.') ? input : input + currentExt;
                    var candidate = Path.Combine(targetDir, newName);
                    var baseNameNoExt = Path.GetFileNameWithoutExtension(newName);
                    var ext = Path.GetExtension(newName);
                    var finalPath = (File.Exists(candidate) || Directory.Exists(candidate))
                        ? GetUniqueFilePath(targetDir, baseNameNoExt, ext)
                        : candidate;

                    if (!string.Equals(finalPath, createdPath, StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(() => File.Move(createdPath, finalPath));
                        App.EventBus.Publish(new FileUpdatedEvent(finalPath));
                    }
                }

                await RefreshFileTree();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddNewFile failed: {ex}");
                await RefreshFileTree();
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling AddNewFile: {e}");
        }
    }

    private async void AddNewFolder(FileNode? node)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentProjectPath)) return;

            string targetDir = GetTargetDirectory(node);
            try
            {
                string createdDir = GetUniqueDirectoryPath(targetDir, "New Folder");
                await Task.Run(() => Directory.CreateDirectory(createdDir));
                App.EventBus.Publish(new FileCreatedEvent(createdDir));

                // Prompt user to rename the newly created folder
                var defaultName = Path.GetFileName(createdDir);
                var input = await PromptForTextAsync("Rename Folder", "Enter new folder name:", defaultName);
                if (!string.IsNullOrWhiteSpace(input))
                {
                    input = input.Trim();
                    string candidate = Path.Combine(targetDir, input);
                    string finalDir = (Directory.Exists(candidate) || File.Exists(candidate))
                        ? GetUniqueDirectoryPath(targetDir, input)
                        : candidate;

                    if (!string.Equals(finalDir, createdDir, StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Run(() => Directory.Move(createdDir, finalDir));
                        App.EventBus.Publish(new FileUpdatedEvent(finalDir));
                    }
                }

                await RefreshFileTree();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddNewFolder failed: {ex}");
                await RefreshFileTree();
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling AddNewFolder: {e}");
        }
    }

    private async void RenameResource(FileNode? node)
    {
        try
        {
            if (node is null || string.IsNullOrEmpty(_currentProjectPath)) return;

            try
            {
                string parentDir = Path.GetDirectoryName(node.FullPath) ?? _currentProjectPath;
                if (string.IsNullOrWhiteSpace(parentDir) || !Directory.Exists(parentDir))
                {
                    parentDir = _currentProjectPath;
                }

                if (node.IsDirectory)
                {
                    await RenameDirectoryAsync(node, parentDir);
                }
                else
                {
                    await RenameFileAsync(node, parentDir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RenameResource failed: {ex}");
                await RefreshFileTree();
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling FileCreatedEvent: {e}");
        }
    }

    private async Task RenameDirectoryAsync(FileNode dirNode, string dirParent)
    {
        var defaultName = dirNode.Name;
        var input = await PromptForTextAsync("Rename Folder", "Enter new folder name:", defaultName);
        if (string.IsNullOrWhiteSpace(input)) return;
        input = input.Trim();

        string candidate = Path.Combine(dirParent, input);
        string newDir = Directory.Exists(candidate) || File.Exists(candidate)
            ? GetUniqueDirectoryPath(dirParent, input)
            : candidate;

        await Task.Run(() => Directory.Move(dirNode.FullPath, newDir));
        App.EventBus.Publish(new FileUpdatedEvent(newDir));
    }

    private async Task RenameFileAsync(FileNode fileNode, string fileParent)
    {
        string currentExt = Path.GetExtension(fileNode.Name);
        string fileName = Path.GetFileName(fileNode.Name);
        var input = await PromptForTextAsync("Rename File", "Enter new file name:", fileName);
        if (string.IsNullOrWhiteSpace(input)) return;
        input = input.Trim();

        string newName = input.Contains('.') ? input : input + currentExt;
        string candidate = Path.Combine(fileParent, newName);

        if (string.Equals(candidate, fileNode.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string baseNameNoExt = Path.GetFileNameWithoutExtension(newName);
        string ext = Path.GetExtension(newName);
        string newFile = (File.Exists(candidate) || Directory.Exists(candidate))
            ? GetUniqueFilePath(fileParent, baseNameNoExt, ext)
            : candidate;

        await Task.Run(() => File.Move(fileNode.FullPath, newFile));
        App.EventBus.Publish(new FileUpdatedEvent(newFile));
    }

    private void Copy(FileNode? node)
    {
        _clipboardPaths.Clear();
        _isCutOperation = false;
        if (node is null) return;
        _clipboardPaths.Add(node.FullPath);
    }

    private void Cut(FileNode? node)
    {
        _clipboardPaths.Clear();
        _isCutOperation = true;
        if (node is null) return;
        _clipboardPaths.Add(node.FullPath);
    }

    private async void Paste(FileNode? node)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentProjectPath) || (_clipboardPaths.Count == 0)) return;

            string destDir;
            var targetDir = GetTargetDirectory(node);
            if (!string.IsNullOrWhiteSpace(targetDir) && Directory.Exists(targetDir))
            {
                destDir = targetDir;
            }
            else
            {
                destDir = _currentProjectPath;
            }
            try
            {
                foreach (var srcPath in _clipboardPaths.ToList())
                {
                    await PasteSingle(srcPath, destDir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Paste failed: {ex}");
            }
            finally
            {
                if (_isCutOperation)
                {
                    _clipboardPaths.Clear();
                    _isCutOperation = false;
                }
                await RefreshFileTree();
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling FileCreatedEvent: {e}");
        }
    }

    private async Task PasteSingle(string srcPath, string destDir)
    {
        var isDir = Directory.Exists(srcPath);
        if (isDir)
        {
            if (_isCutOperation && destDir.StartsWith(srcPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var resolvedDestDir = Path.GetDirectoryName(destDir) ?? destDir;
            var target = _isCutOperation
                ? GetUniqueDirectoryPath(resolvedDestDir, Path.GetFileName(srcPath))
                : GetUniqueDirectoryPath(destDir, Path.GetFileName(srcPath) + " - Copy");

            if (_isCutOperation)
            {
                await Task.Run(() => Directory.Move(srcPath, target));
                App.EventBus.Publish(new FileUpdatedEvent(target));
            }
            else
            {
                await Task.Run(() => CopyDirectoryRecursive(srcPath, target));
                App.EventBus.Publish(new FileCreatedEvent(target));
            }
        }
        else if (File.Exists(srcPath))
        {
            var name = Path.GetFileName(srcPath);
            var nameNoExt = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            var target = _isCutOperation
                ? GetUniqueFilePath(destDir, nameNoExt, ext)
                : GetUniqueFilePath(destDir, nameNoExt + " - Copy", ext);

            if (_isCutOperation)
            {
                await Task.Run(() => File.Move(srcPath, target));
                App.EventBus.Publish(new FileUpdatedEvent(target));
            }
            else
            {
                await Task.Run(() => File.Copy(srcPath, target));
                App.EventBus.Publish(new FileCreatedEvent(target));
            }
        }
    }

    private async void Delete(FileNode? node)
    {
        try
        {
            if (node is null) return;
            if (string.IsNullOrEmpty(_currentProjectPath)) return;

            try
            {
                if (node.IsDirectory)
                {
                    await Task.Run(() => Directory.Delete(node.FullPath, true));
                }
                else
                {
                    await Task.Run(() => File.Delete(node.FullPath));
                }
                App.EventBus.Publish(new FileUpdatedEvent(node.FullPath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete failed: {ex}");
            }
            finally
            {
                await RefreshFileTree();
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling FileCreatedEvent: {e}");
        }
    }

    private string GetTargetDirectory(FileNode? node)
    {
        if (string.IsNullOrEmpty(_currentProjectPath)) return string.Empty;
        string dir;
        if (node is null)
        {
            dir = _currentProjectPath;
        }
        else if (node.IsDirectory)
        {
            dir = node.FullPath;
        }
        else
        {
            string? parent = Path.GetDirectoryName(node.FullPath);
            dir = string.IsNullOrEmpty(parent) ? _currentProjectPath : parent;
        }

        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            return _currentProjectPath;
        }
        return dir;
    }

    private async Task<string?> PromptForTextAsync(string title, string message, string defaultText)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }
        var dlg = new TextInputDialog();
        dlg.Configure(title, message, defaultText);
        return await dlg.ShowDialogAsync(owner);
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    private static string GetUniqueFilePath(string directory, string baseName, string extension)
    {
        string candidate = Path.Combine(directory, baseName + extension);
        int counter = 1;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} ({counter}){extension}");
            counter++;
        }
        return candidate;
    }

    private static string GetUniqueDirectoryPath(string directory, string baseName)
    {
        string candidate = Path.Combine(directory, baseName);
        int counter = 1;
        while (Directory.Exists(candidate) || File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} ({counter})");
            counter++;
        }
        return candidate;
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: false);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var nextDest = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, nextDest);
        }
    }
}
