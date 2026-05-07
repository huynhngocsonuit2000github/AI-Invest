namespace ScrumAgent.ConsoleApp;

public sealed class WorkspaceGuard
{
    public string Root { get; }

    private static readonly string[] DangerousCommandFragments =
    {
        "rm -rf", "del /s", "format ", "diskpart", "shutdown", "sudo ",
        "curl | bash", "wget | bash", "chmod -R 777"
    };

    public WorkspaceGuard(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
    }

    public string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("Path is required.");

        var normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar)
                                     .Replace('/', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(Root, normalized));
        if (!fullPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path outside workspace is blocked: {relativePath}");

        return fullPath;
    }

    public void ValidateCommand(string command, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new InvalidOperationException("Command is required.");

        var lower = command.ToLowerInvariant();
        foreach (var blocked in DangerousCommandFragments)
        {
            if (lower.Contains(blocked, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Dangerous command blocked: {blocked}");
        }

        _ = ResolvePath(workingDirectory);
    }
}
