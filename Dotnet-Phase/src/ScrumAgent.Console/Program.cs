using ScrumAgent.ConsoleApp;
using System.Text.Json;

//var prompt = "Create project planning files for a todo app with users, groups, and roles. Do not create source code yet.";
var prompt = "Create a .NET 8 Web API todo app with users, groups, and roles. Generate source code. Use controller-service-repository structure. Add models, DTOs, EF Core setup, and run dotnet build.";

var settings = LoadSettings();
var workspace = settings.ProjectDir;

Directory.CreateDirectory(workspace);

var guard = new WorkspaceGuard(workspace);
var logger = new WorkspaceLogger(guard);
var tools = new ToolRegistry(new IWorkspaceTool[]
{
    new ListFilesTool(guard),
    new ReadFileTool(guard),
    new WriteFileTool(guard),
    new ReplaceInFileTool(guard),
    new RunCommandTool(guard)
});

using var httpClient = new HttpClient();
var llmClient = new OpenAiResponsesClient(httpClient, settings.ApiKey, settings.BaseUrl, settings.Model);
var loop = new AgentLoop(llmClient, tools, logger, guard);

await loop.RunAsync(prompt, CancellationToken.None);
return 0;

static AgentSettings LoadSettings()
{
    var settingsPath = FindSettingsFile();

    using var stream = File.OpenRead(settingsPath);
    using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    });

    var root = document.RootElement;

    return new AgentSettings(
        RequireSetting(root, "OPENROUTER_API_KEY", settingsPath),
        RequireSetting(root, "LLM_BASE_URL", settingsPath),
        RequireSetting(root, "LLM_MODEL", settingsPath),
        OptionalSetting(root, "PROJECT_DIR") ?? Path.Combine(Directory.GetCurrentDirectory(), "workspace"));
}

static string FindSettingsFile()
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
        Path.Combine(AppContext.BaseDirectory, "appsettings.json")
    };

    foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (File.Exists(candidate))
            return candidate;
    }

    throw new FileNotFoundException("Missing appsettings.json. Create it next to the project or copied output.");
}

static string RequireSetting(JsonElement root, string name, string settingsPath)
{
    var value = OptionalSetting(root, name);
    if (!string.IsNullOrWhiteSpace(value))
        return value;

    throw new InvalidOperationException($"Missing '{name}' in {settingsPath}.");
}

static string? OptionalSetting(JsonElement root, string name)
{
    return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : null;
}

readonly record struct AgentSettings(string ApiKey, string BaseUrl, string Model, string ProjectDir);