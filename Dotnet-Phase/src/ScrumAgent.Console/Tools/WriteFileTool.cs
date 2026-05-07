namespace ScrumAgent.ConsoleApp;

public sealed class WriteFileTool : IWorkspaceTool
{
    private readonly WorkspaceGuard _guard;
    public string Name => "write_file";
    public string Description => "Create or overwrite a UTF-8 text file inside the workspace. Args: path, content.";

    public WriteFileTool(WorkspaceGuard guard) => _guard = guard;

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, string> args, CancellationToken cancellationToken)
    {
        var path = args.GetValueOrDefault("path") ?? throw new InvalidOperationException("path is required");
        var content = args.GetValueOrDefault("content") ?? string.Empty;
        var fullPath = _guard.ResolvePath(path);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        if (File.Exists(fullPath))
        {
            var backupPath = fullPath + ".bak";
            File.Copy(fullPath, backupPath, overwrite: true);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        return new ToolResult(Name, true, $"Wrote {path}");
    }
}
