namespace skt.IDE.Services.Buss;

public class FileCreatedEvent
{
    public string FilePath { get; }
    public string DirectoryPath { get; }

    public FileCreatedEvent(string filePath)
    {
        FilePath = filePath;
        DirectoryPath = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
    }
}

public class FileUpdatedEvent
{
    public string FilePath { get; }

    public FileUpdatedEvent(string filePath)
    {
        FilePath = filePath;
    }
}

public class ProjectLoadedEvent
{
    public string ProjectPath { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public ProjectLoadedEvent(string projectPath, bool success = true, string? errorMessage = null)
    {
        ProjectPath = projectPath;
        Success = success;
        ErrorMessage = errorMessage;
    }
}

public class FileDirtyStateChangedEvent
{
    public string FilePath { get; }
    public bool IsDirty { get; }

    public FileDirtyStateChangedEvent(string filePath, bool isDirty)
    {
        FilePath = filePath;
        IsDirty = isDirty;
    }
}

public class FileOpenedEvent
{
    public string FilePath { get; }

    public FileOpenedEvent(string filePath)
    {
        FilePath = filePath;
    }
}

public class FileClosedEvent
{
    public string FilePath { get; }

    public FileClosedEvent(string filePath)
    {
        FilePath = filePath;
    }
}

public class FileRenamedEvent
{
    public string OldPath { get; }
    public string NewPath { get; }
    public FileRenamedEvent(string oldPath, string newPath)
    {
        OldPath = oldPath;
        NewPath = newPath;
    }
}

public class OpenFileRequestEvent
{
    public string FilePath { get; }

    public OpenFileRequestEvent(string filePath)
    {
        FilePath = filePath;
    }
}

public class CreateFileRequestEvent;

public class ProjectFolderSelectedEvent(string folderPath)
{
    public string FolderPath { get; } = folderPath;
}

public class SaveFileRequestEvent;

public class SaveAsFilesRequestEvent;

public class SelectedDocumentChangedEvent
{
    public string? FilePath { get; }
    public bool HasSelection { get; }
    public bool IsDirty { get; }

    public SelectedDocumentChangedEvent(string? filePath, bool hasSelection, bool isDirty)
    {
        FilePath = filePath;
        HasSelection = hasSelection;
        IsDirty = isDirty;
    }
}

public class SetCaretPositionRequestEvent
{
    public string FilePath { get; }
    public int CaretIndex { get; }

    public SetCaretPositionRequestEvent(string filePath, int caretIndex)
    {
        FilePath = filePath;
        CaretIndex = caretIndex;
    }
}
