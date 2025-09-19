namespace skt.IDE.Services;

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

    public ProjectLoadedEvent(string projectPath)
    {
        ProjectPath = projectPath;
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

public class OpenFileRequestEvent
{
    public string FilePath { get; }

    public OpenFileRequestEvent(string filePath)
    {
        FilePath = filePath;
    }
}

public class CreateFileRequestEvent
{
    public CreateFileRequestEvent()
    {
    }
}

public class ProjectFolderSelectedEvent
{
    public string FolderPath { get; }

    public ProjectFolderSelectedEvent(string folderPath)
    {
        FolderPath = folderPath;
    }
}
