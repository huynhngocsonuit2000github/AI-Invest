namespace ScrumAgent.ConsoleApp;

public sealed class ReplaceInFileTool : IWorkspaceTool
{
    private readonly WorkspaceGuard _guard;
    public string Name => "replace_in_file";
    public string Description => "Replace exact text in a file inside the workspace. Args: path, oldText, newText.";

    public ReplaceInFileTool(WorkspaceGuard guard) => _guard = guard;

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, string> args, CancellationToken cancellationToken)
    {
        var path = args.GetValueOrDefault("path") ?? throw new InvalidOperationException("path is required");
        var oldText = args.GetValueOrDefault("oldText") ?? throw new InvalidOperationException("oldText is required");
        var newText = args.GetValueOrDefault("newText") ?? string.Empty;
        var fullPath = _guard.ResolvePath(path);

        if (!File.Exists(fullPath))
            return new ToolResult(Name, false, string.Empty, $"File not found: {path}");

        var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
        if (!text.Contains(oldText, StringComparison.Ordinal))
            return new ToolResult(Name, false, string.Empty, "oldText was not found.");

        File.Copy(fullPath, fullPath + ".bak", overwrite: true);
        text = text.Replace(oldText, newText, StringComparison.Ordinal);
        await File.WriteAllTextAsync(fullPath, text, cancellationToken);

        return new ToolResult(Name, true, $"Updated {path}");
    }
}
