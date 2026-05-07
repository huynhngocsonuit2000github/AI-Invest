namespace ScrumAgent.ConsoleApp;

public sealed class ListFilesTool : IWorkspaceTool
{
    private readonly WorkspaceGuard _guard;
    public string Name => "list_files";
    public string Description => "List files inside the workspace. Args: path.";

    public ListFilesTool(WorkspaceGuard guard) => _guard = guard;

    public Task<ToolResult> ExecuteAsync(Dictionary<string, string> args, CancellationToken cancellationToken)
    {
        var path = args.GetValueOrDefault("path", ".");
        var fullPath = _guard.ResolvePath(path);

        if (!Directory.Exists(fullPath))
            return Task.FromResult(new ToolResult(Name, false, string.Empty, $"Directory not found: {path}"));

        var entries = Directory.EnumerateFileSystemEntries(fullPath)
            .Select(p => Path.GetRelativePath(_guard.Root, p))
            .OrderBy(x => x)
            .Take(300);

        return Task.FromResult(new ToolResult(Name, true, string.Join(Environment.NewLine, entries)));
    }
}
