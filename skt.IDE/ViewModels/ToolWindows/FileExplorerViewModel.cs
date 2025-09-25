using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Models;
using Avalonia.Threading;
using skt.IDE.Services.Buss;

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
    private readonly object _pendingLock = new();

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
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void DebounceTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.Post(async () =>
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
        });
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

        bool didFullReload = false;

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            string parentDir;
            try
            {
                parentDir = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? _currentProjectPath;
            }
            catch { continue; }

            if (string.IsNullOrEmpty(parentDir)) continue;

            // If change affects project root, fallback to full reload once
            if (string.Equals(parentDir, _currentProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!didFullReload)
                {
                    await LoadProject(_currentProjectPath, announce: false);
                    didFullReload = true;
                }
                continue;
            }

            if (didFullReload) continue; // skip fine-grained if already reloaded

            // Find parent node among existing roots
            FileNode? parentNode = null;
            foreach (var root in RootNodes)
            {
                parentNode = root.FindNodeByPath(parentDir);
                if (parentNode != null) break;
            }

            // If not found, fallback to single full reload
            if (parentNode == null)
            {
                if (!didFullReload)
                {
                    await LoadProject(_currentProjectPath, announce: false);
                    didFullReload = true;
                }
                continue;
            }

            if (parentNode.IsDirectory)
            {
                parentNode.MergeChildrenWithFileSystem();
            }
        }

        if (!didFullReload)
        {
            RestoreTreeState();
        }
        else
        {
            RestoreTreeState();
        }
    }

    private async Task HandleExternalChangesAsync()
    {
        if (string.IsNullOrEmpty(_currentProjectPath) || !Directory.Exists(_currentProjectPath)) return;

        var requested = RequestVisualState?.Invoke();
        string? selected = requested?.selectedPath;
        double offset = requested?.verticalOffset ?? 0.0;

        SaveTreeState(selected, offset);

        // Reload project and restore state after load
        await LoadProject(_currentProjectPath, announce: false);
        RestoreTreeState();
    }

    private void SaveTreeState(string? selectedPath, double verticalOffset)
    {
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in RootNodes)
        {
            CollectExpanded(node, expanded);
        }

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

    private void CollectExpanded(FileNode node, HashSet<string> outSet)
    {
        if (node.IsExpanded)
        {
            outSet.Add(node.FullPath);
        }
        foreach (var child in node.Children)
        {
            CollectExpanded(child, outSet);
        }
    }

    private void RestoreTreeState()
    {
        if (string.IsNullOrEmpty(_currentProjectPath)) return;
        if (!_savedStates.TryGetValue(_currentProjectPath, out var state)) return;

        // restore expanded flags by matching paths
        foreach (var node in RootNodes)
        {
            ApplyExpandedState(node, state.ExpandedPaths);
        }

        // update selected path in VM; view will perform selection+scroll when signaled
        SelectedPath = state.SelectedPath ?? string.Empty;

        // request the view to restore visual selection and scroll
        RestoreVisualStateRequested?.Invoke(state.SelectedPath, state.VerticalOffset);
    }

    private void ApplyExpandedState(FileNode node, HashSet<string> expanded)
    {
        node.IsExpanded = expanded.Contains(node.FullPath);
        foreach (var child in node.Children)
        {
            ApplyExpandedState(child, expanded);
        }
    }

    // TreeState structure
    private class TreeState
    {
        public HashSet<string> ExpandedPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? SelectedPath { get; set; }
        public double VerticalOffset { get; set; }
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

            if (string.IsNullOrEmpty(_currentProjectPath) || !fileEvent.FilePath.StartsWith(_currentProjectPath))
            {
                System.Diagnostics.Debug.WriteLine("File is not in current project - ignoring event");
                return;
            }

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

    public async Task LoadProject(string projectPath, bool announce = true)
    {
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
        {
            ProjectName = NoProjectName;
            _currentProjectPath = string.Empty;
            App.EventBus.Publish(new ProjectLoadedEvent(projectPath, success: false, errorMessage: "Project folder does not exist."));
            App.EventBus.Publish(new StatusBarMessageEvent("Failed to open project: Project folder does not exist.", true));
            return;
        }

        // Save current visual state before we reload the tree
        try
        {
            var visual = RequestVisualState?.Invoke();
            SaveTreeState(visual?.selectedPath, visual?.verticalOffset ?? 0.0);
        }
        catch { }

        _currentProjectPath = projectPath;

        // ensure watcher is started for this project
        EnsureWatcherStarted(projectPath);

        List<FileNode> childNodes = new();
        string projectName = Path.GetFileName(projectPath);

        // Ensure we have a valid project name
        if (string.IsNullOrEmpty(projectName))
        {
            projectName = Path.GetFileName(Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        // Build the file tree off the UI thread
        // Get top-level directory and file paths from disk on a background thread
        List<string> topPaths = new();
        await Task.Run(() =>
        {
            try
            {
                var dirs = Directory.GetDirectories(projectPath).OrderBy(p => p);
                var files = Directory.GetFiles(projectPath).OrderBy(p => p);
                topPaths.AddRange(dirs);
                topPaths.AddRange(files);
            }
            catch
            {
                // ignore IO errors
            }
        });

        // Update RootNodes on UI thread but reuse existing FileNode instances when possible to avoid flashing
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var existingByPath = RootNodes.ToDictionary(n => n.FullPath, n => n, StringComparer.OrdinalIgnoreCase);
            var newRootList = new List<FileNode>(topPaths.Count);

            foreach (var path in topPaths)
            {
                if (existingByPath.TryGetValue(path, out var existing))
                {
                    newRootList.Add(existing);
                    // if it's a directory and was expanded, merge its children to keep instances
                    if (existing.IsDirectory && existing.IsExpanded)
                    {
                        existing.MergeChildrenWithFileSystem();
                    }
                }
                else
                {
                    newRootList.Add(new FileNode(path));
                }
            }

            // replace RootNodes contents in-place
            RootNodes.Clear();
            foreach (var node in newRootList)
            {
                RootNodes.Add(node);
            }
            ProjectName = string.IsNullOrEmpty(projectName) ? NoProjectName : projectName;
        });

        // Publish success event so toolbar and other components can react (e.g. enable New File)
        App.EventBus.Publish(new ProjectLoadedEvent(projectPath, success: true));
        if (announce)
        {
            App.EventBus.Publish(new StatusBarMessageEvent($"Project loaded: {projectName}", 3000));
        }

        // After loading, restore any saved tree state
        RestoreTreeState();
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
                _suppressWatcherReloads = true;
                await Task.Run(() => File.WriteAllText(createdPath, string.Empty));
                App.EventBus.Publish(new FileCreatedEvent(createdPath));
                App.EventBus.Publish(new StatusBarMessageEvent($"Created: {Path.GetFileName(createdPath)}", 3000));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddNewFile failed: {ex}");
            }
            finally
            {
                _suppressWatcherReloads = false;
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
                _suppressWatcherReloads = true;
                await Task.Run(() => Directory.CreateDirectory(createdDir));
                App.EventBus.Publish(new FileCreatedEvent(createdDir));
                App.EventBus.Publish(new StatusBarMessageEvent($"Folder created: {Path.GetFileName(createdDir)}", 3000));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddNewFolder failed: {ex}");
            }
            finally
            {
                _suppressWatcherReloads = false;
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

    public void CancelInlineRename(FileNode node)
    {
        if (node == null) return;
        node.CancelInlineEdit();
    }

    public async void CommitInlineRename(FileNode node)
    {
        if (node == null) return;
        var rawNewName = node.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawNewName))
        {
            node.CancelInlineEdit();
            return;
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            if (rawNewName.Contains(invalidChar))
            {
                App.EventBus.Publish(new StatusBarMessageEvent("Invalid characters in name", true));
                node.CancelInlineEdit();
                return;
            }
        }

        var parentDir = Path.GetDirectoryName(node.FullPath);
        if (string.IsNullOrEmpty(parentDir))
        {
            node.CancelInlineEdit();
            return;
        }

        var oldPath = node.FullPath;
        var newPath = Path.Combine(parentDir, rawNewName);

        bool onlyCaseChanged = !string.Equals(oldPath, newPath, StringComparison.Ordinal) && string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && !onlyCaseChanged)
        {
            node.CompleteInlineEdit();
            return;
        }

        if (!onlyCaseChanged && (Directory.Exists(newPath) || File.Exists(newPath)))
        {
            App.EventBus.Publish(new StatusBarMessageEvent("A file or folder with that name already exists", true));
            node.CancelInlineEdit();
            return;
        }

        try
        {
            _suppressWatcherReloads = true;
            if (node.IsDirectory)
            {
                if (onlyCaseChanged)
                {
                    var temp = newPath + "__temp_case_rename__";
                    Directory.Move(oldPath, temp);
                    Directory.Move(temp, newPath);
                }
                else
                {
                    Directory.Move(oldPath, newPath);
                }
            }
            else
            {
                if (onlyCaseChanged)
                {
                    var temp = newPath + "__temp_case_rename__";
                    File.Move(oldPath, temp);
                    File.Move(temp, newPath);
                }
                else
                {
                    File.Move(oldPath, newPath);
                }
            }
            _suppressWatcherReloads = false;
            node.FullPath = newPath;
            node.CompleteInlineEdit();
            App.EventBus.Publish(new FileUpdatedEvent(newPath));
            App.EventBus.Publish(new StatusBarMessageEvent($"Renamed to: {rawNewName}", 2000));
        }
        catch (Exception ex)
        {
            _suppressWatcherReloads = false;
            System.Diagnostics.Debug.WriteLine($"Rename failed: {ex}");
            App.EventBus.Publish(new StatusBarMessageEvent("Rename failed", true));
            node.CancelInlineEdit();
            return;
        }

        await RefreshFileTree();
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
                _suppressWatcherReloads = true;
                await Task.Run(() => Directory.Move(srcPath, target));
                _suppressWatcherReloads = false;
                App.EventBus.Publish(new FileUpdatedEvent(target));
                App.EventBus.Publish(new StatusBarMessageEvent($"Moved: {Path.GetFileName(target)}", 3000));
            }
            else
            {
                _suppressWatcherReloads = true;
                await Task.Run(() => CopyDirectoryRecursive(srcPath, target));
                _suppressWatcherReloads = false;
                App.EventBus.Publish(new FileCreatedEvent(target));
                App.EventBus.Publish(new StatusBarMessageEvent($"Folder copied: {Path.GetFileName(target)}", 3000));
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
                _suppressWatcherReloads = true;
                await Task.Run(() => File.Move(srcPath, target));
                _suppressWatcherReloads = false;
                App.EventBus.Publish(new FileUpdatedEvent(target));
                App.EventBus.Publish(new StatusBarMessageEvent($"Moved: {Path.GetFileName(target)}", 3000));
            }
            else
            {
                _suppressWatcherReloads = true;
                await Task.Run(() => File.Copy(srcPath, target));
                _suppressWatcherReloads = false;
                App.EventBus.Publish(new FileCreatedEvent(target));
                App.EventBus.Publish(new StatusBarMessageEvent($"Copied: {Path.GetFileName(target)}", 3000));
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
                _suppressWatcherReloads = true;
                if (node.IsDirectory)
                {
                    await Task.Run(() => Directory.Delete(node.FullPath, true));
                }
                else
                {
                    await Task.Run(() => File.Delete(node.FullPath));
                }
                App.EventBus.Publish(new FileUpdatedEvent(node.FullPath));
                App.EventBus.Publish(new StatusBarMessageEvent($"Deleted: {Path.GetFileName(node.FullPath)}", 3000));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete failed: {ex}");
            }
            finally
            {
                _suppressWatcherReloads = false;
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
}
