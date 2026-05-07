using System.Text.Json.Serialization;

namespace ScrumAgent.ConsoleApp;

public sealed record AgentAction(
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("args")] Dictionary<string, string> Args
);

public sealed record AgentResponse(
    [property: JsonPropertyName("thoughtSummary")] string ThoughtSummary,
    [property: JsonPropertyName("actions")] List<AgentAction> Actions,
    [property: JsonPropertyName("done")] bool Done,
    [property: JsonPropertyName("finalAnswer")] string? FinalAnswer
);

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
