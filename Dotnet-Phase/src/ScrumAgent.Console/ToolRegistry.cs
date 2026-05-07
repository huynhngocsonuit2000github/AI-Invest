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

    public async Task<ToolResult> ExecuteAsync(AgentAction action, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(action.Tool, out var tool))
        {
            return new ToolResult(action.Tool, false, string.Empty, $"Unknown tool: {action.Tool}");
        }

        try
        {
            return await tool.ExecuteAsync(action.Args, cancellationToken);
        }
        catch (Exception ex)
        {
            return new ToolResult(action.Tool, false, string.Empty, ex.Message);
        }
    }
}
