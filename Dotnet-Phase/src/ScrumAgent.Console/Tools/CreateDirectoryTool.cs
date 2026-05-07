namespace ScrumAgent.ConsoleApp;

public sealed class CreateDirectoryTool : IWorkspaceTool
{
    private readonly WorkspaceGuard _guard;

    public string Name => "create_directory";

    public string Description => "Create a directory inside the workspace. Args: path.";

    public CreateDirectoryTool(WorkspaceGuard guard) => _guard = guard;

    public Task<ToolResult> ExecuteAsync(Dictionary<string, string> args, CancellationToken cancellationToken)
    {
        var path = args.GetValueOrDefault("path") ?? throw new InvalidOperationException("path is required");
        var fullPath = _guard.ResolvePath(path);

        Directory.CreateDirectory(fullPath);

        return Task.FromResult(new ToolResult(Name, true, $"Created directory {path}"));
    }
}
