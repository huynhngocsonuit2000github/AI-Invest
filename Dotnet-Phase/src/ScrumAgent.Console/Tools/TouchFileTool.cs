namespace ScrumAgent.ConsoleApp;

public sealed class TouchFileTool : IWorkspaceTool
{
    private readonly WorkspaceGuard _guard;

    public string Name => "touch_file";

    public string Description => "Create an empty file inside the workspace if it does not already exist. Args: path.";

    public TouchFileTool(WorkspaceGuard guard) => _guard = guard;

    public Task<ToolResult> ExecuteAsync(Dictionary<string, string> args, CancellationToken cancellationToken)
    {
        var path = args.GetValueOrDefault("path") ?? throw new InvalidOperationException("path is required");
        var fullPath = _guard.ResolvePath(path);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        if (!File.Exists(fullPath))
        {
            using var _ = File.Create(fullPath);
            return Task.FromResult(new ToolResult(Name, true, $"Created file {path}"));
        }

        return Task.FromResult(new ToolResult(Name, true, $"File already exists {path}"));
    }
}
