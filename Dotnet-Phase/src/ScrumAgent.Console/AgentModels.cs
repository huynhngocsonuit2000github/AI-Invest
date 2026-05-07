namespace ScrumAgent.ConsoleApp;

public sealed record AgentResponse
{
    public string? ThoughtSummary { get; init; }

    public List<AgentAction> Actions { get; init; } = [];

    public bool Done { get; init; }

    public string? FinalAnswer { get; init; }
}

public sealed record AgentAction
{
    public string Tool { get; init; } = string.Empty;

    public Dictionary<string, string> Args { get; init; } = [];
}

public sealed record ToolResult(
    string Tool,
    bool Success,
    string Output,
    string? Error = null
);

public sealed record ConversationItem(
    string Role,
    string Content
);