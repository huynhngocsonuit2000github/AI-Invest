namespace ScrumAgent.ConsoleApp;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IWorkspaceTool> _tools;

    public ToolRegistry(IEnumerable<IWorkspaceTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public string DescribeTools()
    {
        return string.Join(Environment.NewLine, _tools.Values.Select(t => $"- {t.Name}: {t.Description}"));
    }

    public async Task<ToolResult> ExecuteAsync(
    AgentAction action,
    CancellationToken cancellationToken)
    {
        if (action is null)
        {
            return new ToolResult("emptytool", false, "Invalid action: action is null.");
        }

        if (string.IsNullOrWhiteSpace(action.Tool))
        {
            return new ToolResult("emptytool", false, "Invalid action: tool is missing or empty.");
        }

        if (!_tools.TryGetValue(action.Tool, out var tool))
        {
            return new ToolResult("emptytool", false, $"Unknown tool: {action.Tool}");
        }

        try
        {
            return await tool.ExecuteAsync(action.Args!, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(action.Tool, false, string.Empty, ex.Message);
        }
    }
}
