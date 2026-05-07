namespace ScrumAgent.ConsoleApp;

public sealed class WorkspaceLogger
{
    private readonly WorkspaceGuard _guard;

    public WorkspaceLogger(WorkspaceGuard guard)
    {
        _guard = guard;
    }

    public async Task WriteAsync(string relativePath, string content, CancellationToken cancellationToken)
    {
        var path = _guard.ResolvePath(Path.Combine("logs", relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    public async Task AppendAsync(string relativePath, string content, CancellationToken cancellationToken)
    {
        var path = _guard.ResolvePath(Path.Combine("logs", relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.AppendAllTextAsync(path, content, cancellationToken);
    }
}
