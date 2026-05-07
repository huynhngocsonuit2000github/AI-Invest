using System.Text.Json;

namespace ScrumAgent.ConsoleApp;

public sealed class AgentLoop
{
    private readonly OpenAiResponsesClient _llmClient;
    private readonly ToolRegistry _tools;
    private readonly WorkspaceLogger _logger;
    private readonly WorkspaceGuard _guard;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public AgentLoop(OpenAiResponsesClient llmClient, ToolRegistry tools, WorkspaceLogger logger, WorkspaceGuard guard)
    {
        _llmClient = llmClient;
        _tools = tools;
        _logger = logger;
        _guard = guard;
    }

    public async Task RunAsync(string userPrompt, CancellationToken cancellationToken)
    {
        var messages = new List<ConversationItem>
        {
            new("developer", BuildDeveloperPrompt()),
            new("user", userPrompt)
        };

        for (var iteration = 1; iteration <= 20; iteration++)
        {
            System.Console.WriteLine($"\n--- Iteration {iteration} ---");
            var raw = await _llmClient.CompleteAsync(messages, cancellationToken);
            await _logger.WriteAsync($"task_runs/iteration-{iteration:00}-model.txt", raw, cancellationToken);

            var agentResponse = ParseAgentResponse(raw);
            System.Console.WriteLine(agentResponse.ThoughtSummary);

            if (agentResponse.Done)
            {
                System.Console.WriteLine("\nDone.");
                System.Console.WriteLine(agentResponse.FinalAnswer ?? string.Empty);
                await _logger.WriteAsync("task_runs/final-answer.md", agentResponse.FinalAnswer ?? string.Empty, cancellationToken);
                return;
            }

            if (agentResponse.Actions.Count == 0)
            {
                messages.Add(new("user", "No actions were provided. Return either actions or done=true."));
                continue;
            }

            var results = new List<ToolResult>();
            foreach (var action in agentResponse.Actions.Take(5))
            {
                System.Console.WriteLine($"Running tool: {action.Tool}");
                var result = await _tools.ExecuteAsync(action, cancellationToken);
                results.Add(result);
                await _logger.AppendAsync("task_runs/tool-results.md", FormatResult(result), cancellationToken);
            }

            var resultJson = JsonSerializer.Serialize(results, _jsonOptions);
            messages.Add(new("assistant", raw));
            messages.Add(new("user", "Tool results:\n" + resultJson + "\nContinue. If the task is complete, return done=true."));
        }

        System.Console.WriteLine("Stopped after max iterations.");
    }

    private string BuildDeveloperPrompt()
    {
        return $$"""
You are Phase 1 of a local AI Scrum Team Agent.
Your role in this MVP is Developer Agent + basic Scrum executor.

Workspace root:
{{_guard.Root}}

Available tools:
{{_tools.DescribeTools()}}

Rules:
- Work only inside the workspace.
- Prefer small files and small edits.
- Use write_file to create task, memory, and source files.
- Use replace_in_file for targeted updates.
- Use run_command only for safe commands such as dotnet, git, npm, mkdir, ls, dir.
- After source code changes, run build/test if possible.
- Create logs or task files under scrum/ and logs/ when useful.
- Do not claim success unless the required tool output proves it.

Return JSON only. No markdown outside JSON.

JSON shape:
{
  "thoughtSummary": "short summary, no private chain of thought",
  "actions": [
    {
      "tool": "tool_name",
      "args": {
        "path": "example",
        "content": "example"
      }
    }
  ],
  "done": false,
  "finalAnswer": null
}

When finished:
{
  "thoughtSummary": "finished summary",
  "actions": [],
  "done": true,
  "finalAnswer": "what was done, commands run, files changed, any limitation"
}
""";
    }

    private AgentResponse ParseAgentResponse(string raw)
    {
        var json = ExtractJsonObject(raw);
        var parsed = JsonSerializer.Deserialize<AgentResponse>(json, _jsonOptions);
        if (parsed is null)
            throw new InvalidOperationException("Model response could not be parsed as AgentResponse.");
        return parsed;
    }

    private static string ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                return trimmed[firstBrace..(lastBrace + 1)];
        }

        return trimmed;
    }

    private static string FormatResult(ToolResult result)
    {
        return $"""
## {DateTimeOffset.UtcNow:u} {result.Tool}

Success: {result.Success}

Output:
```text
{result.Output}
```

Error:
```text
{result.Error}
```

""";
    }
}
