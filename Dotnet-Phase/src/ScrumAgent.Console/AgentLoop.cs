using System.Text.Json;

namespace ScrumAgent.ConsoleApp;

public sealed class AgentLoop
{
    private const int MaxToolOutputCharsForModel = 3000;
    private const int MaxToolErrorCharsForModel = 1500;
    private const int MaxInvalidResponsePreviewChars = 1200;

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
        var baseMessages = new List<ConversationItem>
        {
            new("developer", BuildDeveloperPrompt()),
            new("user", userPrompt)
        };
        var messages = new List<ConversationItem>(baseMessages);

        var dateTime = DateTime.UtcNow;
        var executedToolCount = 0;
        var wroteSourceFile = false;
        var buildSucceeded = false;

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
                messages = BuildNextMessages(baseMessages, BuildInvalidResponsePrompt(userPrompt, raw));
                continue;
            }

            agentResponse = agentResponse with
            {
                Actions = agentResponse.Actions
                    .Where(x => !string.IsNullOrWhiteSpace(x.Tool))
                    .ToList()
            };
            System.Console.WriteLine(agentResponse.ThoughtSummary);

            if (TryBuildForbiddenSourceActionError(userPrompt, agentResponse.Actions, out var forbiddenSourceError))
            {
                await _logger.AppendAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/blocked-actions.md", forbiddenSourceError, cancellationToken);
                messages = BuildNextMessages(baseMessages, forbiddenSourceError);
                continue;
            }

            if (TryBuildGenericProjectNameActionError(agentResponse.Actions, out var genericProjectNameError))
            {
                await _logger.AppendAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/blocked-actions.md", genericProjectNameError, cancellationToken);
                messages = BuildNextMessages(baseMessages, genericProjectNameError);
                continue;
            }

            if (agentResponse.Actions.Count == 0)
            {
                string? completionError = null;
                if (agentResponse.Done && executedToolCount > 0 && CanFinish(userPrompt, wroteSourceFile, buildSucceeded, out completionError))
                {
                    var finalAnswer = BuildFinalAnswer(agentResponse, []);
                    System.Console.WriteLine("\nDone.");
                    System.Console.WriteLine(finalAnswer);
                    await _logger.WriteAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/final-answer.md", finalAnswer, cancellationToken);
                    return;
                }

                messages = BuildNextMessages(baseMessages, completionError ?? BuildNoActionPrompt(executedToolCount));
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
                wroteSourceFile = wroteSourceFile || DidWriteSourceFile(action, result);
                buildSucceeded = buildSucceeded || DidBuildSucceed(action, result);
                await _logger.AppendAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/tool-results.md", FormatResult(result), cancellationToken);
            }

            var resultJson = JsonSerializer.Serialize(CompactResultsForModel(results), _jsonOptions);
            string? completionErrorAfterActions = null;

            if (agentResponse.Done
                && actionsToRun.Count == agentResponse.Actions.Count
                && results.All(x => x.Success)
                && CanFinish(userPrompt, wroteSourceFile, buildSucceeded, out completionErrorAfterActions))
            {
                var finalAnswer = BuildFinalAnswer(agentResponse, results);
                System.Console.WriteLine("\nDone.");
                System.Console.WriteLine(finalAnswer);
                await _logger.WriteAsync($"task_runs/{dateTime:yyyyMMddHHmmss}/final-answer.md", finalAnswer, cancellationToken);
                return;
            }

            messages = BuildNextMessages(
                baseMessages,
                completionErrorAfterActions ?? BuildContinuationPrompt(resultJson, actionsToRun.Count, agentResponse.Actions.Count));
        }

        System.Console.WriteLine("Stopped after max iterations.");
    }

    private static List<ConversationItem> BuildNextMessages(
        IReadOnlyList<ConversationItem> baseMessages,
        string continuationPrompt)
    {
        var messages = new List<ConversationItem>(baseMessages)
        {
            new("user", continuationPrompt)
        };
        return messages;
    }

    private string BuildDeveloperPrompt()
    {
        return $$"""
You are a local AI Scrum Team Agent.
Your role is Developer Agent + basic Scrum executor.

You are not a chatbot.
You must execute work by returning JSON tool actions.
Do not explain steps to the user unless the task is finished.

Workspace root:
{{_guard.Root}}

Available tools:
{{_tools.DescribeTools()}}

STRICT OUTPUT RULES:
- Return JSON only.
- No markdown outside JSON.
- No code fences.
- Do not say "Sure".
- Do not say "Below is".
- Do not return manual instructions.
- The first character must be { and the last character must be }.
- Actions must use tool/args objects.
- Every action must use the field name "tool".
- Never use "tool_name".
- Never use shorthand keys like mkdir, touch, create_folder, create_file, or file_manager.
- Do not invent tools.
- Use only tools listed in Available tools.

GENERAL RULES:
- Work only inside the workspace.
- All paths must be relative to the workspace root.
- Use forward slashes in paths.
- Prefer small files and small edits.
- Use create_directory to create folders.
- Use write_file to create task, memory, docs, and source files.
- Use touch_file only when an empty placeholder file is required.
- Use replace_in_file for targeted updates.
- Use run_command only for safe commands such as dotnet, git, npm, mkdir, ls, dir.
- run_command workingDirectory must already exist. Use "." for commands that create new folders with -o or output paths.
- After source code changes, run build/test if possible.
- Create logs or task files under scrum/ and logs/ when useful.
- Do not claim success unless the required tool output proves it.

PLANNING REQUEST RULES:
When the user asks to create planning files, initialize project planning automatically.
Do not ask the user for detailed file content.
Generate meaningful content yourself based on the user's project description.

For planning-only requests:
- Do not create application source code.
- Do not create source directories or files.
- Do not run dotnet commands.
- Create useful docs, backlog tasks, and memory files.
- Do not create only one generic planning document.
- Do not create empty markdown files.
- Do not create empty memory files unless the user explicitly asks for empty files.
- Use descriptive kebab-case filenames.
- Never use placeholder filenames such as example.md, task-specific-file-name.md, placeholder.md, or file.md.

For a new software project planning request, create this structure when useful:
- scrum/backlog
- scrum/sprint
- scrum/done
- docs/product
- docs/architecture
- docs/api
- docs/decisions
- memory
- logs/task_runs
- logs/build
- logs/tests
- logs/reviews

Create planning documents in these categories when relevant:
- product requirements under docs/product/
- architecture or design under docs/architecture/
- API, contract, or interface plan under docs/api/
- technical decisions under docs/decisions/
- backlog task files under scrum/backlog/
- project memory under memory/

Recommended planning document pattern:
- docs/product/{project-name}-product-spec.md
- docs/architecture/{project-name}-architecture.md
- docs/api/{project-name}-api-plan.md

If the project has no API or external interface, skip API-specific documents.
If the project has no authentication requirement, do not create auth-specific tasks.
If the user provides a tech stack, follow it.
If not, infer a reasonable stack from the project type.

PROJECT NAMING RULES:
- Derive a stable PascalCase project name from the user's product name or domain.
- For a todo app, use TodoApp.
- Never use generic names such as App, MyApp, Sample, Demo, Project, or Application as the project name unless the user explicitly names the product that way.
- .NET project, namespace, folder, and solution names must use the derived project name consistently.

.NET BACKEND STRUCTURE RULES:
- For new .NET backend/API projects, prefer a simple single-project structure first.
- Create one Web API project and put implementation folders inside that project:
  - source/{ProjectName}.sln
  - source/{ProjectName}.Api/{ProjectName}.Api.csproj
  - source/{ProjectName}.Api/Controllers
  - source/{ProjectName}.Api/Services
  - source/{ProjectName}.Api/Repositories
  - source/{ProjectName}.Api/Models
  - source/{ProjectName}.Api/Dtos
  - source/{ProjectName}.Api/Data
  - source/{ProjectName}.Api/Auth
  - source/{ProjectName}.Api/Tests
- Do not create separate class library projects for Application, Domain, Infrastructure, Services, Repositories, or Tests during initial setup.
- Do not create source/src/{ProjectName}.Application, source/src/{ProjectName}.Domain, source/src/{ProjectName}.Infrastructure, source/tests/{ProjectName}.Tests, source/App.Services, or source/App.Repositories.
- Put services, repositories, DTOs, entities/models, DbContext, auth helpers, and tests in folders inside the single API project until the user asks to split projects.

BACKLOG TASK RULES:
- Create 5 to 10 backlog task files depending on project complexity.
- Use sequential task ids: TASK-0001, TASK-0002, TASK-0003.
- TASK-0001 must set up the actual source project, not only folders.
- Later tasks must map to the user's requested features, modules, or domain behavior.
- Do not hardcode todo, user, group, role, or auth tasks unless the user requested them.
- Every newly created backlog task must use Status: Ready.
- Do not mark newly created backlog tasks as Done.

Recommended backlog task pattern:
- scrum/backlog/TASK-0001-project-setup.md
- scrum/backlog/TASK-0002-{domain-module-or-feature}.md
- scrum/backlog/TASK-0003-{domain-module-or-feature}.md
- scrum/backlog/TASK-0004-{domain-module-or-feature}.md
- scrum/backlog/TASK-0005-tests.md

TASK-0001 must include:
- create one actual source project
- create one project file or package manifest
- create application entry point
- create configuration file when relevant
- create base folders inside the one project
- create initial health or smoke-test endpoint when relevant
- configure basic framework services
- run build
- use the explicit requested framework, for example `--framework net8.0` for .NET 8
- create only setup/skeleton code, not business modules
- do not create separate class library projects
- do not create the initial database schema, EF migrations, or run `dotnet ef database update`; those belong to persistence or feature implementation tasks after entities exist

Infer task names from the user's requested domain and features.
Examples:
- ecommerce: product, cart, order, payment, customer
- CRM: customer, contact, lead, activity, reporting
- todo or task management: user, group, role, todo, auth
- blog or CMS: post, category, media, comment, author
- booking system: resource, availability, reservation, payment, notification
Do not use these examples unless they match the user's request.

TASK EXECUTION RULES:
When the user asks to implement a task by task id, such as "Implement TASK-0001":
- Search scrum/backlog, scrum/sprint, and scrum/done for the matching task file.
- Read the matching task file first.
- If the task file status is Done, do not re-implement it unless the user explicitly asks to redo it.
- Read relevant docs under docs/.
- Read relevant memory files under memory/.
- Infer the required implementation from the task file, architecture docs, and memory.
- Do not ask the user to repeat task details if the task file exists.
- Implement only that task.
- Do not implement future tasks.
- After implementation, run build/test if possible.
- Update memory/project_map.json and memory/feature_history.md after successful build.
- If the task cannot be implemented because information is missing, create a clear note in logs and finalAnswer.

NEXT TASK RULES:
When the user says "implement next task" or "pick the first backlog task":
- List files in scrum/backlog.
- Select the lowest TASK number with Status: Ready or Status: Todo.
- Read that task file.
- Read relevant docs and memory.
- Implement only that task.
- After successful build/test, update the task status to Done or move/copy the task file to scrum/done if supported by available tools.
- Do not skip tasks unless the task is already Done.

CONTENT QUALITY RULES:
Each markdown planning file must contain useful generated content.
Each markdown planning file should include clear headings and bullet points.

Product spec should include:
- Overview
- Target users
- Main features
- Business rules
- User stories
- Acceptance criteria
- Out of scope

Architecture or design document should include:
- Tech stack
- Application layers
- Folder structure
- Database or storage plan when relevant
- Authentication and authorization plan when relevant
- Dependency flow
- Build/test strategy

API or interface plan should include:
- Planned endpoints or interfaces
- Request/response conventions when relevant
- Validation rules
- Error response conventions
- Authentication requirements when relevant

Each backlog task file must include:
- Goal
- Scope
- Technical Context
- Expected Files or Areas
- Technical Tasks
- Acceptance Criteria
- Test Plan
- Dependencies
- Non-Goals
- Status

Backlog task files must be detailed enough for a developer agent to implement without asking for clarification:
- Include concrete class, interface, folder, endpoint, DTO, database table, migration, configuration, and test-file suggestions when relevant.
- Describe expected behavior, validation rules, authorization rules, data relationships, and error cases.
- Technical Tasks must be specific implementation steps, not generic lines such as "create service" or "create repository".
- Acceptance Criteria must be observable and testable.
- Test Plan must name the unit, integration, or API tests expected for the task.
- For planning-only requests, describe source files and commands as future work inside task text only; do not create source files or run commands.
- Expected Files or Areas must list concrete file paths, not vague entries such as "database schema".
- If a task needs database work, name the future entity, DbContext, configuration, migration, and repository files explicitly.

MEMORY FILE RULES:
Create or update these memory files when useful:
- memory/project_map.json
- memory/business_rules.md
- memory/feature_history.md
- memory/test_knowledge.md
- memory/coding_conventions.md
- memory/known_issues.md
- memory/agent_notes.md

Memory files must contain useful initial project knowledge:
- project_map.json must be valid JSON
- business_rules.md must summarize domain rules
- feature_history.md must start with the planning/bootstrap entry
- test_knowledge.md must describe testing approach
- coding_conventions.md must describe coding standards
- known_issues.md must say no known issues if none exist
- agent_notes.md must describe what was initialized and the next recommended task

DEFAULT TECHNICAL ASSUMPTIONS:
If the user does not specify a stack, infer from the project type.
For backend API projects, prefer:
- .NET 8 Web API
- PostgreSQL with EF Core
- JWT authentication only if authentication is required or requested
- controller-service-repository architecture
- DTOs for API requests/responses
- async methods
- xUnit tests

For frontend web apps, prefer:
- React or Next.js
- TypeScript
- component-based structure
- API client layer
- basic tests

If the user specifies a stack, follow the user's stack instead.

CODING REQUEST RULES:
When the user asks to create or update source code:
- Read relevant planning files and memory first when they exist.
- Implement only the requested task or scope.
- Create/update source files under source/.
- Follow the architecture and coding conventions from docs and memory.
- If the task file uses generic App.* paths, vague "database schema" entries, or conflicts with the architecture docs, update the task/planning files to the concrete project structure first, then implement the corrected task.
- For .NET 8 projects, pass `--framework net8.0` to `dotnet new` commands and use package versions compatible with .NET 8.
- Do not run EF migrations or database updates until the task explicitly defines entities and DbContext files needed for that migration.
- Keep controllers or handlers thin when applicable.
- Put business logic in services when applicable.
- Put data access in repositories when applicable.
- Use DTOs or request/response models for API contracts when applicable.
- Use async methods when appropriate.
- Run build/test after source changes if possible.
- Update memory only after successful build/test when possible.

JSON RESPONSE SHAPE:
{
  "thoughtSummary": "short summary, no private chain of thought",
  "actions": [
    {
      "tool": "write_file",
      "args": {
        "path": "example.md",
        "content": "example content"
      }
    }
  ],
  "done": false,
  "finalAnswer": null
}

EXAMPLE CREATE DIRECTORY ACTION:
{
  "tool": "create_directory",
  "args": {
    "path": "docs/product"
  }
}

EXAMPLE WRITE FILE ACTION:
{
  "tool": "write_file",
  "args": {
    "path": "docs/product/sample-product-spec.md",
    "content": "# Product Spec\n\n## Overview\nDescribe the product goal and main users.\n"
  }
}

EXAMPLE READ FILE ACTION:
{
  "tool": "read_file",
  "args": {
    "path": "scrum/backlog/TASK-0001-project-setup.md"
  }
}

EXAMPLE RUN COMMAND ACTION:
{
  "tool": "run_command",
  "args": {
    "command": "dotnet build source/TodoApp.sln",
    "workingDirectory": "."
  }
}

WHEN FINISHED:
{
  "thoughtSummary": "finished summary",
  "actions": [],
  "done": true,
  "finalAnswer": "what was done, commands run, files changed, any limitation"
}
""";
    }

    private static string BuildInvalidResponsePrompt(string userPrompt, string rawResponse)
    {
        var actionGuidance = IsSourceCreationForbidden(userPrompt)
            ? "Use write_file/create_directory actions only under docs/, scrum/, memory/, or logs/. Do not use source/ paths or dotnet commands."
            : "Use tool actions to continue the original request.";
        var responsePreview = TruncateForModel(rawResponse.ReplaceLineEndings(" ").Trim(), MaxInvalidResponsePreviewChars);

        return $$"""
Your previous response was not valid JSON for the required schema.
Return exactly one JSON object and no other text.
The first character must be { and the last character must be }.
{{actionGuidance}}

Invalid response preview:
{{responsePreview}}


Use this shape:
{
  "thoughtSummary": "short summary",
  "actions": [
    {
      "tool": "write_file",
      "args": {
        "path": "docs/product/todo-app-product-spec.md",
        "content": "real content here"
      }
    }
  ],
  "done": false,
  "finalAnswer": null
}
""";
    }

    private static string BuildNoActionPrompt(int executedToolCount)
    {
        return $$"""
No executable tool actions were provided. Tool executions so far: {{executedToolCount}}.
Do not set done=true until tool results prove the task is complete.
Continue the original user request by returning write_file/create_directory actions.
Use descriptive task-specific filenames. Never use literal placeholder filenames such as example.md, task-specific-file-name.md, or placeholder.md.
""";
    }

    private static bool CanFinish(string userPrompt, bool wroteSourceFile, bool buildSucceeded, out string? error)
    {
        error = null;

        if (IsSourceCreationForbidden(userPrompt))
            return true;

        if (!IsImplementationPrompt(userPrompt))
            return true;

        if (!wroteSourceFile)
        {
            error = """
The task is not complete. No source files were written under source/.
Return tool actions that create or update the requested project files under source/, then run the required build command.
""";
            return false;
        }

        if (RequiresBuild(userPrompt) && !buildSucceeded)
        {
            error = """
The task is not complete. The prompt requires running a build, but no successful build tool result has been recorded.
Return a run_command action for the build, using the correct project workingDirectory.
""";
            return false;
        }

        return true;
    }

    private static bool IsImplementationPrompt(string prompt)
    {
        return !IsSourceCreationForbidden(prompt)
            && (prompt.Contains("implement", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("create a .net", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("create source code", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("under source/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresBuild(string prompt)
    {
        return prompt.Contains("build", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DidWriteSourceFile(AgentAction action, ToolResult result)
    {
        return result.Success
            && action.Tool.Equals("write_file", StringComparison.OrdinalIgnoreCase)
            && action.Args.TryGetValue("path", out var path)
            && path.Replace('\\', '/').StartsWith("source/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DidBuildSucceed(AgentAction action, ToolResult result)
    {
        return result.Success
            && action.Tool.Equals("run_command", StringComparison.OrdinalIgnoreCase)
            && action.Args.TryGetValue("command", out var command)
            && command.Contains("dotnet build", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryBuildForbiddenSourceActionError(
        string userPrompt,
        IReadOnlyList<AgentAction> actions,
        out string error)
    {
        error = string.Empty;

        if (!IsSourceCreationForbidden(userPrompt))
            return false;

        var forbiddenActions = actions
            .Where(IsSourceCreationAction)
            .Select(DescribeAction)
            .ToArray();

        if (forbiddenActions.Length == 0)
            return false;

        error = $"""
The user explicitly requested planning only and said not to create source code yet.
These proposed actions are blocked and were not executed:
{string.Join(Environment.NewLine, forbiddenActions.Select(x => "- " + x))}

Return only planning actions under docs/, scrum/, memory/, or logs/.
Do not write files under source/.
Do not create source directories.
Do not run dotnet new, dotnet build, dotnet add, dotnet restore, or other implementation commands.
""";
        return true;
    }

    private static bool IsSourceCreationForbidden(string prompt)
    {
        return prompt.Contains("do not create source code", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("do not create application source code", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("do not create code", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("no any code", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("just planning", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSourceCreationAction(AgentAction action)
    {
        var tool = action.Tool;
        var path = action.Args.TryGetValue("path", out var actionPath)
            ? actionPath.Replace('\\', '/')
            : string.Empty;
        var workingDirectory = action.Args.TryGetValue("workingDirectory", out var wd)
            ? wd.Replace('\\', '/')
            : string.Empty;
        var command = action.Args.TryGetValue("command", out var cmd)
            ? cmd
            : string.Empty;

        if ((tool.Equals("write_file", StringComparison.OrdinalIgnoreCase)
                || tool.Equals("create_directory", StringComparison.OrdinalIgnoreCase)
                || tool.Equals("touch_file", StringComparison.OrdinalIgnoreCase)
                || tool.Equals("replace_in_file", StringComparison.OrdinalIgnoreCase))
            && path.StartsWith("source/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!tool.Equals("run_command", StringComparison.OrdinalIgnoreCase))
            return false;

        return workingDirectory.StartsWith("source/", StringComparison.OrdinalIgnoreCase)
            || command.Contains("dotnet new", StringComparison.OrdinalIgnoreCase)
            || command.Contains("dotnet build", StringComparison.OrdinalIgnoreCase)
            || command.Contains("dotnet add", StringComparison.OrdinalIgnoreCase)
            || command.Contains("dotnet restore", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeAction(AgentAction action)
    {
        if (action.Args.TryGetValue("path", out var path))
            return $"{action.Tool} {path}";

        if (action.Args.TryGetValue("command", out var command))
            return $"{action.Tool} {command}";

        return action.Tool;
    }

    private static bool TryBuildGenericProjectNameActionError(
        IReadOnlyList<AgentAction> actions,
        out string error)
    {
        error = string.Empty;

        var genericProjectActions = actions
            .Where(IsGenericProjectNameAction)
            .Select(DescribeAction)
            .ToArray();

        if (genericProjectActions.Length == 0)
            return false;

        error = $"""
These proposed actions use a blocked generic or multi-project structure and were not executed:
{string.Join(Environment.NewLine, genericProjectActions.Select(x => "- " + x))}

Use a concrete project name derived from the requested product or architecture documents.
For the todo app, use a single TodoApp.Api project with folders inside it.
Do not create source/App.Api, source/App.Services, source/App.Repositories, source/App.Tests, TodoApp.Application, TodoApp.Domain, TodoApp.Infrastructure, or TodoApp.Tests projects.
If a task file still contains generic App.* paths, update the task file first before implementing it.
""";
        return true;
    }

    private static bool IsGenericProjectNameAction(AgentAction action)
    {
        var path = action.Args.TryGetValue("path", out var actionPath)
            ? actionPath.Replace('\\', '/')
            : string.Empty;
        var workingDirectory = action.Args.TryGetValue("workingDirectory", out var wd)
            ? wd.Replace('\\', '/')
            : string.Empty;
        var command = action.Args.TryGetValue("command", out var cmd)
            ? cmd.Replace('\\', '/')
            : string.Empty;

        return ContainsGenericAppProjectPath(path)
            || ContainsGenericAppProjectPath(workingDirectory)
            || ContainsGenericAppProjectPath(command);
    }

    private static bool ContainsGenericAppProjectPath(string value)
    {
        return ContainsGenericAppProjectToken(value.Replace('\\', '/'));
    }

    private static bool ContainsGenericAppProjectToken(string value)
    {
        var genericNames = new[]
        {
            "App.Api",
            "App.Services",
            "App.Repositories",
            "App.Tests",
            "TodoApp.Application",
            "TodoApp.Domain",
            "TodoApp.Infrastructure",
            "TodoApp.Tests"
        };

        foreach (var genericName in genericNames)
        {
            for (var index = value.IndexOf(genericName, StringComparison.OrdinalIgnoreCase);
                 index >= 0;
                 index = value.IndexOf(genericName, index + genericName.Length, StringComparison.OrdinalIgnoreCase))
            {
                var before = index == 0 ? '\0' : value[index - 1];
                var afterIndex = index + genericName.Length;
                var after = afterIndex >= value.Length ? '\0' : value[afterIndex];

                if (!IsProjectNameCharacter(before) && !IsProjectNameCharacter(after))
                    return true;
            }
        }

        return value.Contains("source/src/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("source/tests/", StringComparison.OrdinalIgnoreCase)
            || value.Contains(" src/", StringComparison.OrdinalIgnoreCase)
            || value.Contains(" tests/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectNameCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '.';
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

    private static IReadOnlyList<ModelToolResult> CompactResultsForModel(IEnumerable<ToolResult> results)
    {
        return results
            .Select(result => new ModelToolResult(
                result.Tool,
                result.Success,
                TruncateForModel(result.Output, MaxToolOutputCharsForModel),
                string.IsNullOrWhiteSpace(result.Error)
                    ? null
                    : TruncateForModel(result.Error, MaxToolErrorCharsForModel)))
            .ToArray();
    }

    private static string TruncateForModel(string? value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            return value ?? string.Empty;

        return value[..maxChars] + $"\n...[truncated {value.Length - maxChars} chars; full output is in logs]";
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

    private sealed record ModelToolResult(
        string Tool,
        bool Success,
        string Output,
        string? Error);
}
