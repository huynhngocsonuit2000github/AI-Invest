using System.Diagnostics;
using System.Text;

namespace ScrumAgent.ConsoleApp;

public sealed class RunCommandTool : IWorkspaceTool
{
    private readonly WorkspaceGuard _guard;
    public string Name => "run_command";
    public string Description => "Run an approved shell command inside the workspace. Args: command, workingDirectory.";

    public RunCommandTool(WorkspaceGuard guard) => _guard = guard;

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, string> args, CancellationToken cancellationToken)
    {
        var command = args.GetValueOrDefault("command") ?? throw new InvalidOperationException("command is required");
        var workingDirectory = args.GetValueOrDefault("workingDirectory", ".");
        _guard.ValidateCommand(command, workingDirectory);
        var fullWorkingDirectory = _guard.ResolvePath(workingDirectory);

        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-lc \"{EscapeForBash(command)}\"",
            WorkingDirectory = fullWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ToolResult(Name, false, output.ToString(), "Command timed out after 5 minutes.");
        }

        var combined = output.ToString();
        var err = error.ToString();
        if (combined.Length > 20000) combined = combined[..20000] + "\n...[truncated]";
        if (err.Length > 10000) err = err[..10000] + "\n...[truncated]";

        return process.ExitCode == 0
            ? new ToolResult(Name, true, combined)
            : new ToolResult(Name, false, combined, err);
    }

    private static string EscapeForBash(string command)
    {
        return command.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
