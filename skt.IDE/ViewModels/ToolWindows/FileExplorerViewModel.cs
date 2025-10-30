using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Models;
using Avalonia.Threading;
using skt.IDE.Services.Buss;
using CommunityToolkit.Mvvm.Messaging;
using Timer = System.Timers.Timer;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class FileExplorerViewModel : ObservableObject
{
    private const String NoProjectName = "Open A Project to Begin";
    
    [ObservableProperty]
    private ObservableCollection<FileNode> _rootNodes = new();

    [ObservableProperty]
    private string _projectName = NoProjectName;

    [ObservableProperty]
    private string _selectedPath = string.Empty; // last selected path

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

    public event Action<FileNode>? InlineRenameStarted;

    // Visual state exchange with the view
    // View should set this delegate to return current selected path and current vertical scroll offset
    public Func<(string? selectedPath, double verticalOffset)>? RequestVisualState { get; set; }
    // View should subscribe to this to restore selection/scroll after a reload
    public event Action<string?, double>? RestoreVisualStateRequested;

    private readonly Dictionary<string, TreeState> _savedStates = new();

    private FileSystemWatcher? _watcher;
    private readonly Timer _debounceTimer;

    // When true, filesystem watcher events are ignored (used for internal operations)
    private volatile bool _suppressWatcherReloads;

    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _pendingLock = new();

    public FileExplorerViewModel()
    {
        ProjectName = NoProjectName;
        App.Messenger.Register<FileCreatedEvent>(this, (r, m) => OnFileCreated(m));
        App.Messenger.Register<FileUpdatedEvent>(this, (r, m) => OnFileUpdated(m));
        App.Messenger.Register<CreateFileRequestEvent>(this, (r, m) => AddNewFile(null));
        App.Messenger.Register<ProjectFolderSelectedEvent>(this, (r, m) => OnProjectFolderSelected(m));
        AddNewFileCommand = new RelayCommand<FileNode>(AddNewFile);
        AddNewFolderCommand = new RelayCommand<FileNode>(AddNewFolder);
        RenameResourceCommand = new RelayCommand<FileNode>(RenameResource);
        CopyCommand = new RelayCommand<FileNode>(Copy);
        CutCommand = new RelayCommand<FileNode>(Cut);
        PasteCommand = new RelayCommand<FileNode>(Paste);
        DeleteCommand = new RelayCommand<FileNode>(Delete);

        _debounceTimer = new Timer(500) { AutoReset = false };
        _debounceTimer.Elapsed += DebounceTimer_Elapsed;
    }

    private void EnsureWatcherStarted(string folder)
    {
        try
        {
            if (_watcher != null)
            {
                if (string.Equals(_watcher.Path, folder, StringComparison.OrdinalIgnoreCase)) return;
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            _watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            _watcher.Changed += OnFsEvent;
            _watcher.Created += OnFsEvent;
            _watcher.Deleted += OnFsEvent;
            _watcher.Renamed += OnFsRenamed;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start FileSystemWatcher: {ex}");
        }
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (_suppressWatcherReloads) return;
        lock (_pendingLock)
        {
            _pendingPaths.Add(e.FullPath);
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnFsRenamed(object sender, RenamedEventArgs e)
    {
        if (_suppressWatcherReloads) return;
        lock (_pendingLock)
        {
            _pendingPaths.Add(e.FullPath);
            _pendingPaths.Add(e.OldFullPath);
        }
        // Emit rename event if both old and new paths are inside current project (external rename)
        if (!string.IsNullOrEmpty(_currentProjectPath)
            && e.OldFullPath.StartsWith(_currentProjectPath, StringComparison.OrdinalIgnoreCase)
            && e.FullPath.StartsWith(_currentProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            App.Messenger.Send(new FileRenamedEvent(e.OldFullPath, e.FullPath));
        }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void DebounceTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _ = SafeProcessPendingChangesAsync());
    }

    private async Task SafeProcessPendingChangesAsync()
    {
        try
        {
            await ProcessPendingFileChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing pending FS changes: {ex}");
            // fallback to full reload if something went wrong
            await HandleExternalChangesAsync();
        }
    }

    private async Task ProcessPendingFileChangesAsync()
    {
        if (string.IsNullOrEmpty(_currentProjectPath) || !Directory.Exists(_currentProjectPath)) return;

        List<string> paths;
        lock (_pendingLock)
        {
            if (_pendingPaths.Count == 0) return;
            paths = _pendingPaths.ToList();
            _pendingPaths.Clear();
        }

        // large batch or too many distinct directories -> full reload
        if (paths.Count > 50)
        {
            await HandleExternalChangesAsync();
            return;
        }

        // capture visual state once
        var requested = RequestVisualState?.Invoke();
        SaveTreeState(requested?.selectedPath, requested?.verticalOffset ?? 0.0);

        var parentDirs = ComputeParentDirs(paths);

        if (parentDirs.Any(d => string.Equals(d, _currentProjectPath, StringComparison.OrdinalIgnoreCase)))
        {
            await LoadProject(_currentProjectPath, announce: false);
            RestoreTreeState();
            return;
        }

        foreach (var dir in parentDirs)
        {
            var parentNode = RootNodes.Select(root => root.FindNodeByPath(dir)).FirstOrDefault(n => n != null);
            if (parentNode == null)
            {
                await LoadProject(_currentProjectPath, announce: false);
                RestoreTreeState();
                return;
            }
            if (parentNode.IsDirectory) parentNode.MergeChildrenWithFileSystem();
        }

        RestoreTreeState();
    }

    private List<string> ComputeParentDirs(IEnumerable<string> paths)
        => paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Directory.Exists(p) ? p : Path.GetDirectoryName(p) ?? _currentProjectPath)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task HandleExternalChangesAsync()
    {
        if (string.IsNullOrEmpty(_currentProjectPath) || !Directory.Exists(_currentProjectPath)) return;

        var visual = RequestVisualState?.Invoke();
        SaveTreeState(visual?.selectedPath, visual?.verticalOffset ?? 0.0);

        await LoadProject(_currentProjectPath, announce: false);
        RestoreTreeState();
    }

    private void SaveTreeState(string? selectedPath, double verticalOffset)
    {
        var expanded = RootNodes
            .SelectMany(EnumerateExpanded)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var state = new TreeState
        {
            ExpandedPaths = expanded,
            SelectedPath = selectedPath,
            VerticalOffset = verticalOffset
        };

        if (!string.IsNullOrEmpty(_currentProjectPath))
        {
            _savedStates[_currentProjectPath] = state;
        }
    }

    private IEnumerable<string> EnumerateExpanded(FileNode node)
    {
        if (node.IsExpanded) yield return node.FullPath;
        foreach (var child in node.Children.SelectMany(EnumerateExpanded)) yield return child;
    }

    private void RestoreTreeState()
    {
        if (string.IsNullOrEmpty(_currentProjectPath)) return;
        if (!_savedStates.TryGetValue(_currentProjectPath, out var state)) return;

        foreach (var node in RootNodes)
        {
            ApplyExpandedState(node, state.ExpandedPaths);
        }

        SelectedPath = state.SelectedPath ?? string.Empty;
        RestoreVisualStateRequested?.Invoke(state.SelectedPath, state.VerticalOffset);
    }

    private void ApplyExpandedState(FileNode node, HashSet<string> expanded)
    {
        if (node.IsPlaceholder) return;

        bool shouldBeExpanded = expanded.Contains(node.FullPath);
        node.IsExpanded = shouldBeExpanded;

        if (shouldBeExpanded && node.IsDirectory)
        {
            foreach (var child in node.Children.Where(c => !c.IsPlaceholder))
            {
                ApplyExpandedState(child, expanded);
            }
        }
    }

    private sealed class TreeState
    {
        public HashSet<string> ExpandedPaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public string? SelectedPath { get; init; }
        public double VerticalOffset { get; init; }
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

            if (string.IsNullOrEmpty(_currentProjectPath) || !fileEvent.FilePath.StartsWith(_currentProjectPath)) return;

            System.Diagnostics.Debug.WriteLine("Refreshing file tree...");
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await LoadProject(_currentProjectPath, announce: false);
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

            if (string.IsNullOrEmpty(_currentProjectPath) || !fileEvent.FilePath.StartsWith(_currentProjectPath)) return;

            System.Diagnostics.Debug.WriteLine("Refreshing file tree due to file update...");
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await LoadProject(_currentProjectPath, announce: false);
            });
            System.Diagnostics.Debug.WriteLine("File tree refresh completed after update");
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling FileCreatedEvent: {e}");
        }
    }

    private async Task LoadProject(string projectPath, bool announce = true)
    {
        if (!IsValidProjectPath(projectPath))
        {
            HandleInvalidProjectPath(projectPath);
            return;
        }

        try
        {
            var visual = RequestVisualState?.Invoke();
            SaveTreeState(visual?.selectedPath, visual?.verticalOffset ?? 0.0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Visual state capture failed: {ex}");
        }

        _currentProjectPath = projectPath;
        EnsureWatcherStarted(projectPath);

        var topPaths = await BuildTopPathsAsync(projectPath);
        await Dispatcher.UIThread.InvokeAsync(() => UpdateRootNodes(topPaths, projectPath));

        PublishProjectLoaded(projectPath, announce);
        RestoreTreeState();
    }

    private static bool IsValidProjectPath(string projectPath)
        => !string.IsNullOrEmpty(projectPath) && Directory.Exists(projectPath);

    private void HandleInvalidProjectPath(string projectPath)
    {
        ProjectName = NoProjectName;
        _currentProjectPath = string.Empty;
        App.Messenger.Send(new ProjectLoadedEvent(projectPath, success: false, errorMessage: "Project folder does not exist."));
        App.Messenger.Send(new StatusBarMessageEvent("Failed to open project: Project folder does not exist.", true));
    }

    private async Task<List<string>> BuildTopPathsAsync(string projectPath)
    {
        var result = new List<string>();
        await Task.Run(() =>
        {
            try
            {
                result.AddRange(Directory.GetDirectories(projectPath).OrderBy(p => p));
                result.AddRange(Directory.GetFiles(projectPath).OrderBy(p => p));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Enumerating top paths failed: {ex}");
            }
        });
        return result;
    }

    private void UpdateRootNodes(IEnumerable<string> topPaths, string projectPath)
    {
        var existingByPath = RootNodes.ToDictionary(n => n.FullPath, n => n, StringComparer.OrdinalIgnoreCase);
        var newRootList = topPaths
            .Select(path => existingByPath.TryGetValue(path, out var existing) ? existing : new FileNode(path))
            .ToList();

        foreach (var node in newRootList.Where(n => n is { IsDirectory: true, IsExpanded: true }))
        {
            node.MergeChildrenWithFileSystem();
        }

        RootNodes.Clear();
        foreach (var node in newRootList) RootNodes.Add(node);

        var name = Path.GetFileName(projectPath);
        if (string.IsNullOrEmpty(name))
        {
            var normalized = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            name = Path.GetFileName(normalized);
        }
        ProjectName = string.IsNullOrEmpty(name) ? NoProjectName : name;
    }

    private void PublishProjectLoaded(string projectPath, bool announce)
    {
        App.Messenger.Send(new ProjectLoadedEvent(projectPath, success: true));
        if (announce)
        {
            App.Messenger.Send(new StatusBarMessageEvent($"Project loaded: {Path.GetFileName(projectPath)}", 3000));
        }
    }

    private async Task RefreshFileTree()
    {
        if (!string.IsNullOrEmpty(_currentProjectPath))
        {
            await LoadProject(_currentProjectPath, announce: false);
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
                await WithWatcherSuppressedAsync(() => Task.Run(() => File.WriteAllText(createdPath, string.Empty)));
                App.Messenger.Send(new FileCreatedEvent(createdPath));
                App.Messenger.Send(new StatusBarMessageEvent($"Created: {Path.GetFileName(createdPath)}", 3000));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddNewFile failed: {ex}");
            }
            finally
            {
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
                await WithWatcherSuppressedAsync(() => Task.Run(() => Directory.CreateDirectory(createdDir)));
                App.Messenger.Send(new FileCreatedEvent(createdDir));
                App.Messenger.Send(new StatusBarMessageEvent($"Folder created: {Path.GetFileName(createdDir)}", 3000));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddNewFolder failed: {ex}");
            }
            finally
            {
                await RefreshFileTree();
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling AddNewFolder: {e}");
        }
    }

    private void RenameResource(FileNode? node)
    {
        try
        {
            if (node is null || string.IsNullOrEmpty(_currentProjectPath)) return;
            node.BeginInlineEdit();
            InlineRenameStarted?.Invoke(node);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Error initiating inline rename: {e}");
        }
    }

    public void CancelInlineRename(FileNode? node)
    {
        if (node == null) return;
        node.CancelInlineEdit();
    }

    public async Task<bool> CommitInlineRename(FileNode node)
    {
        if (node == null) return false;
        var rawNewName = node.Name.Trim();
        if (string.IsNullOrWhiteSpace(rawNewName))
        {
            node.CancelInlineEdit();
            return false;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (invalidChars.Any(rawNewName.Contains))
        {
            App.Messenger.Send(new StatusBarMessageEvent("Invalid characters in name", true));
            node.CancelInlineEdit();
            return false;
        }

        var parentDir = Path.GetDirectoryName(node.FullPath);
        if (string.IsNullOrEmpty(parentDir))
        {
            node.CancelInlineEdit();
            return false;
        }

        var oldPath = node.FullPath;
        var newPath = Path.Combine(parentDir, rawNewName);

        bool onlyCaseChanged = !string.Equals(oldPath, newPath, StringComparison.Ordinal) && string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && !onlyCaseChanged)
        {
            node.CompleteInlineEdit();
            return true;
        }

        if (!onlyCaseChanged && (Directory.Exists(newPath) || File.Exists(newPath)))
        {
            App.Messenger.Send(new StatusBarMessageEvent("A file or folder with that name already exists", true));
            node.CancelInlineEdit();
            return false;
        }

        try
        {
            await WithWatcherSuppressedAsync(() => Task.Run(() => MoveWithCaseHandling(oldPath, newPath, node.IsDirectory, onlyCaseChanged)));
            node.FullPath = newPath;
            node.CompleteInlineEdit();
            App.Messenger.Send(new FileRenamedEvent(oldPath, newPath));
            App.Messenger.Send(new FileUpdatedEvent(newPath));
            App.Messenger.Send(new StatusBarMessageEvent($"Renamed to: {rawNewName}", 2000));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Rename failed: {ex}");
            App.Messenger.Send(new StatusBarMessageEvent("Rename failed", true));
            return false;
        }
        finally
        {
            await RefreshFileTree();
        }
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

            var destDir = Directory.Exists(GetTargetDirectory(node)) ? GetTargetDirectory(node) : _currentProjectPath;
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
            if (_isCutOperation && destDir.StartsWith(srcPath, StringComparison.OrdinalIgnoreCase)) return;

            var resolvedDestDir = Path.GetDirectoryName(destDir) ?? destDir;
            var target = _isCutOperation
                ? GetUniqueDirectoryPath(resolvedDestDir, Path.GetFileName(srcPath))
                : GetUniqueDirectoryPath(destDir, Path.GetFileName(srcPath) + " - Copy");

            if (_isCutOperation)
            {
                await WithWatcherSuppressedAsync(() => Task.Run(() => Directory.Move(srcPath, target)));
                App.Messenger.Send(new FileUpdatedEvent(target));
                App.Messenger.Send(new StatusBarMessageEvent($"Moved: {Path.GetFileName(target)}", 3000));
            }
            else
            {
                await WithWatcherSuppressedAsync(() => Task.Run(() => CopyDirectoryRecursive(srcPath, target)));
                App.Messenger.Send(new FileCreatedEvent(target));
                App.Messenger.Send(new StatusBarMessageEvent($"Folder copied: {Path.GetFileName(target)}", 3000));
            }
            return;
        }

        if (File.Exists(srcPath))
        {
            var name = Path.GetFileName(srcPath);
            var nameNoExt = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            var target = _isCutOperation
                ? GetUniqueFilePath(destDir, nameNoExt, ext)
                : GetUniqueFilePath(destDir, nameNoExt + " - Copy", ext);

            if (_isCutOperation)
            {
                await WithWatcherSuppressedAsync(() => Task.Run(() => File.Move(srcPath, target)));
                App.Messenger.Send(new FileUpdatedEvent(target));
                App.Messenger.Send(new StatusBarMessageEvent($"Moved: {Path.GetFileName(target)}", 3000));
            }
            else
            {
                await WithWatcherSuppressedAsync(() => Task.Run(() => File.Copy(srcPath, target)));
                App.Messenger.Send(new FileCreatedEvent(target));
                App.Messenger.Send(new StatusBarMessageEvent($"Copied: {Path.GetFileName(target)}", 3000));
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
                await WithWatcherSuppressedAsync(() =>
                    Task.Run(() =>
                    {
                        if (node.IsDirectory) Directory.Delete(node.FullPath, true);
                        else File.Delete(node.FullPath);
                    }));
                App.Messenger.Send(new FileUpdatedEvent(node.FullPath));
                App.Messenger.Send(new StatusBarMessageEvent($"Deleted: {Path.GetFileName(node.FullPath)}", 3000));
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

    private static void MoveWithCaseHandling(string oldPath, string newPath, bool isDirectory, bool onlyCaseChanged)
    {
        if (onlyCaseChanged)
        {
            var temp = newPath + "__temp_case_rename__";
            if (isDirectory) Directory.Move(oldPath, temp); else File.Move(oldPath, temp);
            if (isDirectory) Directory.Move(temp, newPath); else File.Move(temp, newPath);
            return;
        }

        if (isDirectory) Directory.Move(oldPath, newPath); else File.Move(oldPath, newPath);
    }

    private async Task WithWatcherSuppressedAsync(Func<Task> action)
    {
        try
        {
            _suppressWatcherReloads = true;
            await action();
        }
        finally
        {
            _suppressWatcherReloads = false;
        }
    }
}
