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

        var dateTime = DateTime.UtcNow;
        var executedToolCount = 0;

        for (var iteration = 1; iteration <= 20; iteration++)
        {
            System.Console.WriteLine($"\n--- Iteration {iteration} ---");
            var raw = await _llmClient.CompleteAsync(messages, cancellationToken);
            await _logger.WriteAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/iteration-{iteration:00}-model.txt", raw, cancellationToken);

            AgentResponse agentResponse;
            try
            {
                agentResponse = ParseAgentResponse(raw);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                var error = $"Invalid model response: {ex.Message}";
                System.Console.WriteLine(error);
                await _logger.AppendAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/parse-errors.md", FormatParseError(iteration, raw, error), cancellationToken);
                messages.Add(new("assistant", raw));
                messages.Add(new("user", BuildInvalidResponsePrompt(userPrompt)));
                continue;
            }

            agentResponse = agentResponse with
            {
                Actions = agentResponse.Actions
                    .Where(x => !string.IsNullOrWhiteSpace(x.Tool))
                    .ToList()
            };
            System.Console.WriteLine(agentResponse.ThoughtSummary);

            if (agentResponse.Actions.Count == 0)
            {
                if (agentResponse.Done && executedToolCount > 0)
                {
                    var finalAnswer = BuildFinalAnswer(agentResponse, []);
                    System.Console.WriteLine("\nDone.");
                    System.Console.WriteLine(finalAnswer);
                    await _logger.WriteAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/final-answer.md", finalAnswer, cancellationToken);
                    return;
                }

                messages.Add(new("user", BuildNoActionPrompt(userPrompt, executedToolCount)));
                continue;
            }

            var results = new List<ToolResult>();
            var actionsToRun = agentResponse.Actions;
            foreach (var action in actionsToRun)
            {
                System.Console.WriteLine($"Running tool: {action.Tool}");
                var result = await _tools.ExecuteAsync(action, cancellationToken);
                results.Add(result);
                executedToolCount++;
                await _logger.AppendAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/tool-results.md", FormatResult(result), cancellationToken);
            }

            var resultJson = JsonSerializer.Serialize(results, _jsonOptions);

            if (agentResponse.Done && actionsToRun.Count == agentResponse.Actions.Count && results.All(x => x.Success))
            {
                var finalAnswer = BuildFinalAnswer(agentResponse, results);
                System.Console.WriteLine("\nDone.");
                System.Console.WriteLine(finalAnswer);
                await _logger.WriteAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/final-answer.md", finalAnswer, cancellationToken);
                return;
            }

            messages.Add(new("assistant", raw));
            messages.Add(new("user", BuildContinuationPrompt(resultJson, actionsToRun.Count, agentResponse.Actions.Count)));
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
- Use create_directory to create folders.
- Use touch_file only when an empty placeholder file is required.
- Use replace_in_file for targeted updates.
- Use run_command only for safe commands such as dotnet, git, npm, mkdir, ls, dir.
- After source code changes, run build/test if possible.
- Create logs or task files under scrum/ and logs/ when useful.
- For project planning tasks, create task-specific planning files under docs/ and scrum/. Use descriptive filenames, never example.md.
- Do not claim success unless the required tool output proves it.

Return JSON only. No markdown outside JSON.
The first character must be { and the last character must be }.
Actions must use tool/args objects. Never use shorthand keys like mkdir or touch.

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

    private static string BuildInvalidResponsePrompt(string userPrompt)
    {
        return $$"""
Your previous response was not valid JSON for the required schema.
Return exactly one JSON object and no other text.
The first character must be { and the last character must be }.
You must continue the original user request by using tool actions.

Original user request:
{{userPrompt}}

Use this shape:
{
  "thoughtSummary": "short summary",
  "actions": [
    {
      "tool": "write_file",
      "args": {
        "path": "docs/product/task-specific-file-name.md",
        "content": "content here"
      }
    }
  ],
  "done": false,
  "finalAnswer": null
}
""";
    }

    private static string BuildNoActionPrompt(string userPrompt, int executedToolCount)
    {
        return $$"""
No executable tool actions were provided. Tool executions so far: {{executedToolCount}}.
Do not set done=true until tool results prove the task is complete.
Continue the original user request by returning write_file/create_directory actions.
Use task-specific filenames. Never use example.md.

Original user request:
{{userPrompt}}
""";
    }

    private AgentResponse ParseAgentResponse(string raw)
    {
        var json = ExtractJsonObject(raw);
        using var document = JsonDocument.Parse(json);

        return new AgentResponse
        {
            ThoughtSummary = TryGetStringProperty(document.RootElement, "thoughtSummary", out var thoughtSummary)
                ? thoughtSummary
                : null,
            Actions = ParseActions(document.RootElement),
            Done = TryGetBooleanProperty(document.RootElement, "done", out var done) && done,
            FinalAnswer = TryGetStringProperty(document.RootElement, "finalAnswer", out var finalAnswer)
                ? finalAnswer
                : null
        };
    }

    private static List<AgentAction> ParseActions(JsonElement root)
    {
        if (!root.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            return [];

        var parsedActions = new List<AgentAction>();

        foreach (var action in actions.EnumerateArray())
        {
            if (action.ValueKind == JsonValueKind.String)
            {
                parsedActions.AddRange(ParseCommandActions(action.GetString()));
                continue;
            }

            if (action.ValueKind != JsonValueKind.Object)
                continue;

            if (TryGetStringProperty(action, "tool", out var tool))
            {
                parsedActions.Add(new AgentAction
                {
                    Tool = tool,
                    Args = ParseArgs(action)
                });
                continue;
            }

            if (TryParseTypedAction(action, out var typedAction))
            {
                parsedActions.Add(typedAction);
                continue;
            }

            if (TryGetStringProperty(action, "mkdir", out var directoryPath))
            {
                parsedActions.Add(new AgentAction
                {
                    Tool = "create_directory",
                    Args = new Dictionary<string, string> { ["path"] = directoryPath }
                });
                continue;
            }

            if (TryGetStringProperty(action, "touch", out var filePath))
            {
                parsedActions.Add(new AgentAction
                {
                    Tool = "touch_file",
                    Args = new Dictionary<string, string>
                    {
                        ["path"] = filePath
                    }
                });
            }
        }

        return parsedActions;
    }

    private static bool TryParseTypedAction(JsonElement action, out AgentAction parsedAction)
    {
        parsedAction = new AgentAction();

        if (!TryGetStringProperty(action, "type", out var type))
            return false;

        if (!TryGetStringProperty(action, "path", out var path))
            return false;

        if (string.Equals(type, "create_folder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "create_directory", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "mkdir", StringComparison.OrdinalIgnoreCase))
        {
            parsedAction = new AgentAction
            {
                Tool = "create_directory",
                Args = new Dictionary<string, string> { ["path"] = path }
            };
            return true;
        }

        if (string.Equals(type, "create_file", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "touch_file", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "touch", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetStringProperty(action, "content", out var content))
            {
                parsedAction = new AgentAction
                {
                    Tool = "write_file",
                    Args = new Dictionary<string, string>
                    {
                        ["path"] = path,
                        ["content"] = content
                    }
                };
                return true;
            }

            parsedAction = new AgentAction
            {
                Tool = "touch_file",
                Args = new Dictionary<string, string> { ["path"] = path }
            };
            return true;
        }

        if (string.Equals(type, "write_file", StringComparison.OrdinalIgnoreCase))
        {
            parsedAction = new AgentAction
            {
                Tool = "write_file",
                Args = new Dictionary<string, string>
                {
                    ["path"] = path,
                    ["content"] = TryGetStringProperty(action, "content", out var content) ? content : string.Empty
                }
            };
            return true;
        }

        return false;
    }

    private static IEnumerable<AgentAction> ParseCommandActions(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            yield break;

        var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            yield break;

        if (string.Equals(parts[0], "mkdir", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var path in parts.Skip(1).Where(x => !x.StartsWith("-", StringComparison.Ordinal)))
            {
                yield return new AgentAction
                {
                    Tool = "create_directory",
                    Args = new Dictionary<string, string> { ["path"] = path }
                };
            }
        }

        if (string.Equals(parts[0], "touch", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var path in parts.Skip(1).Where(x => !x.StartsWith("-", StringComparison.Ordinal)))
            {
                yield return new AgentAction
                {
                    Tool = "touch_file",
                    Args = new Dictionary<string, string> { ["path"] = path }
                };
            }
        }
    }

    private static Dictionary<string, string> ParseArgs(JsonElement action)
    {
        if (!action.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Object)
            return [];

        var parsedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in args.EnumerateObject())
        {
            parsedArgs[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText();
        }

        return parsedArgs;
    }

    private static bool TryGetStringProperty(JsonElement element, string name, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetBooleanProperty(JsonElement element, string name, out bool value)
    {
        value = false;

        if (!element.TryGetProperty(name, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
            return true;

        return false;
    }

    private static string ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Model returned an empty response.");

        JsonException? lastJsonException = null;

        for (var start = raw.IndexOf('{'); start >= 0; start = raw.IndexOf('{', start + 1))
        {
            if (!TryExtractBalancedJsonObject(raw, start, out var candidate))
                continue;

            try
            {
                using var document = JsonDocument.Parse(candidate);

                if (IsAgentResponseObject(document.RootElement))
                    return candidate;
            }
            catch (JsonException ex)
            {
                lastJsonException = ex;
            }
        }

        if (lastJsonException is not null)
            throw new InvalidOperationException($"No valid agent JSON object found in model response. Last JSON error: {lastJsonException.Message}. Preview: {Preview(raw)}");

        throw new InvalidOperationException($"No agent JSON object found in model response. Preview: {Preview(raw)}");
    }

    private static bool TryExtractBalancedJsonObject(string raw, int start, out string json)
    {
        json = string.Empty;
        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < raw.Length; i++)
        {
            var c = raw[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\')
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;

            if (c == '}')
            {
                depth--;

                if (depth == 0)
                {
                    json = raw[start..(i + 1)];
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAgentResponseObject(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && (root.TryGetProperty("actions", out _)
                || root.TryGetProperty("done", out _)
                || root.TryGetProperty("thoughtSummary", out _)
                || root.TryGetProperty("finalAnswer", out _));
    }

    private static string Preview(string raw)
    {
        var normalized = raw.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 240
            ? normalized
            : normalized[..240] + "...";
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

    private static string BuildContinuationPrompt(string resultJson, int executedActionCount, int totalActionCount)
    {
        if (executedActionCount < totalActionCount)
        {
            return $"""
Tool results:
{resultJson}

Only {executedActionCount} of {totalActionCount} actions were executed in this batch.
Return the remaining actions using the required tool/args JSON schema.
Set done=false until all required actions have been executed.
""";
        }

        return "Tool results:\n" + resultJson + "\nContinue. If the task is complete, return done=true.";
    }

    private static string BuildFinalAnswer(AgentResponse response, IReadOnlyList<ToolResult> results)
    {
        if (!string.IsNullOrWhiteSpace(response.FinalAnswer))
            return response.FinalAnswer;

        if (results.Count == 0)
            return "Task completed. No final summary was provided by the model.";

        var successfulOutputs = results
            .Where(x => x.Success && !string.IsNullOrWhiteSpace(x.Output))
            .Select(x => $"- {x.Output}")
            .ToArray();

        if (successfulOutputs.Length == 0)
            return "Task completed. Tool actions finished without a model-provided final summary.";

        return "Task completed. Tool results:\n" + string.Join(Environment.NewLine, successfulOutputs);
    }

    private static string FormatParseError(int iteration, string raw, string error)
    {
        return $"""
## {DateTimeOffset.UtcNow:u} Iteration {iteration}

Error:
```text
{error}
```

Raw response:
```text
{raw}
```

""";
    }
}