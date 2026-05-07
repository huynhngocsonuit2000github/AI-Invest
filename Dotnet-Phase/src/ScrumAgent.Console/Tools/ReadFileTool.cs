namespace ScrumAgent.ConsoleApp;

public sealed class ReadFileTool : IWorkspaceTool
{
    private readonly WorkspaceGuard _guard;
    public string Name => "read_file";
    public string Description => "Read a UTF-8 text file inside the workspace. Args: path.";

    public ReadFileTool(WorkspaceGuard guard) => _guard = guard;

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, string> args, CancellationToken cancellationToken)
    {
        var path = args.GetValueOrDefault("path") ?? throw new InvalidOperationException("path is required");
        var fullPath = _guard.ResolvePath(path);

        if (!File.Exists(fullPath))
            return new ToolResult(Name, false, string.Empty, $"File not found: {path}");

        var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
        if (text.Length > 20000)
            text = text[..20000] + "\n...[truncated]";

        return new ToolResult(Name, true, text);
    }
}
