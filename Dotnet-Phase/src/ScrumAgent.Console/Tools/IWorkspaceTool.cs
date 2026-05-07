namespace ScrumAgent.ConsoleApp;

public interface IWorkspaceTool
{
    string Name { get; }
    string Description { get; }

    Task<ToolResult> ExecuteAsync(Dictionary<string, string> args, CancellationToken cancellationToken);
}