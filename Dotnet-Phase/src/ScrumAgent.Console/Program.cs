using ScrumAgent.ConsoleApp;
using System.Text.Json;

var settings = LoadSettings();
var prompt = await LoadPromptAsync(settings.PromptPath, CancellationToken.None);
var workspace = settings.ProjectDir;

Directory.CreateDirectory(workspace);
BootstrapWorkspace(workspace);

var guard = new WorkspaceGuard(workspace);
var logger = new WorkspaceLogger(guard);

var tools = new ToolRegistry(new IWorkspaceTool[]
{
    new ListFilesTool(guard),
    new ReadFileTool(guard),
    new CreateDirectoryTool(guard),
    new TouchFileTool(guard),
    new WriteFileTool(guard),
    new ReplaceInFileTool(guard),
    new RunCommandTool(guard)
});

using var httpClient = new HttpClient();
var llmClient = new OpenAiResponsesClient(httpClient, settings.Provider, settings.ApiKey, settings.BaseUrl, settings.Model);
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
    var provider = NormalizeProvider(OptionalSetting(root, "LLM_PROVIDER") ?? "openrouter");
    var hasProviderSettings = TryGetProviderSettings(root, provider, out var providerSettings);
    var apiKey = OptionalProviderSetting(hasProviderSettings, providerSettings, "ApiKey")
        ?? OptionalSetting(root, "LLM_API_KEY")
        ?? OptionalSetting(root, "OPENROUTER_API_KEY");
    var baseUrl = OptionalProviderSetting(hasProviderSettings, providerSettings, "BaseUrl")
        ?? OptionalSetting(root, "LLM_BASE_URL")
        ?? DefaultBaseUrl(provider);
    var model = OptionalProviderSetting(hasProviderSettings, providerSettings, "Model")
        ?? OptionalSetting(root, "LLM_MODEL");

    if (RequiresApiKey(provider) && string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException($"Missing API key for '{provider}' in {settingsPath}.");

    if (string.IsNullOrWhiteSpace(model))
        throw new InvalidOperationException($"Missing model for '{provider}' in {settingsPath}.");

    return new AgentSettings(
        provider,
        apiKey,
        baseUrl,
        model,
        OptionalSetting(root, "PROJECT_DIR") ?? Path.Combine(Directory.GetCurrentDirectory(), "workspace"),
        OptionalSetting(root, "PROMPT_FILE") ?? "prompt.txt");
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

static string NormalizeProvider(string provider)
{
    var normalized = provider.Trim().ToLowerInvariant();

    return normalized switch
    {
        "openrouter" => normalized,
        "ollama" => normalized,
        _ => throw new InvalidOperationException("LLM_PROVIDER must be either 'openrouter' or 'ollama'.")
    };
}

static bool TryGetProviderSettings(JsonElement root, string provider, out JsonElement providerSettings)
{
    providerSettings = default;

    if (!root.TryGetProperty("LLM_PROVIDERS", out var providers) || providers.ValueKind != JsonValueKind.Object)
        return false;

    foreach (var item in providers.EnumerateObject())
    {
        if (!string.Equals(item.Name, provider, StringComparison.OrdinalIgnoreCase))
            continue;

        providerSettings = item.Value;
        return providerSettings.ValueKind == JsonValueKind.Object;
    }

    return false;
}

static string? OptionalProviderSetting(bool hasProviderSettings, JsonElement providerSettings, string name)
{
    return hasProviderSettings
        ? OptionalSetting(providerSettings, name)
        : null;
}

static string DefaultBaseUrl(string provider)
{
    return provider switch
    {
        "openrouter" => "https://openrouter.ai/api/v1",
        "ollama" => "http://localhost:11434/v1",
        _ => throw new InvalidOperationException($"Unsupported LLM provider '{provider}'.")
    };
}

static bool RequiresApiKey(string provider)
{
    return provider == "openrouter";
}

static string? OptionalSetting(JsonElement root, string name)
{
    return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : null;
}

static async Task<string> LoadPromptAsync(string promptPath, CancellationToken cancellationToken)
{
    var path = ResolveConfigFile(promptPath);

    if (!File.Exists(path))
        throw new FileNotFoundException($"Missing prompt file: {path}");

    var prompt = await File.ReadAllTextAsync(path, cancellationToken);
    if (string.IsNullOrWhiteSpace(prompt))
        throw new InvalidOperationException($"Prompt file is empty: {path}");

    return prompt.Trim();
}

static string ResolveConfigFile(string path)
{
    if (Path.IsPathFullyQualified(path))
        return path;

    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), path),
        Path.Combine(AppContext.BaseDirectory, path)
    };

    return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
}

static void BootstrapWorkspace(string workspace)
{
    var directories = new[]
    {
        "source",
        "scrum/backlog",
        "scrum/sprint",
        "scrum/done",
        "docs/product",
        "docs/architecture",
        "docs/api",
        "docs/decisions",
        "memory",
        "logs/task_runs",
        "logs/build",
        "logs/tests",
        "logs/reviews"
    };

    foreach (var directory in directories)
    {
        Directory.CreateDirectory(Path.Combine(workspace, directory));
    }

    WriteFileIfMissing(workspace, "memory/project_map.json", """
{
  "projectName": "AI Scrum Project",
  "sourceRoot": "source",
  "scrumRoot": "scrum",
  "docsRoot": "docs"
}
""");
    WriteFileIfMissing(workspace, "memory/business_rules.md", "# Business Rules\n\n");
    WriteFileIfMissing(workspace, "memory/feature_history.md", "# Feature History\n\n");
    WriteFileIfMissing(workspace, "memory/test_knowledge.md", "# Test Knowledge\n\n");
    WriteFileIfMissing(workspace, "memory/coding_conventions.md", "# Coding Conventions\n\n");
    WriteFileIfMissing(workspace, "memory/known_issues.md", "# Known Issues\n\n");
    WriteFileIfMissing(workspace, "memory/agent_notes.md", "# Agent Notes\n\n");
}

static void WriteFileIfMissing(string workspace, string relativePath, string content)
{
    var path = Path.Combine(workspace, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

    if (!File.Exists(path))
    {
        File.WriteAllText(path, content);
    }
}

readonly record struct AgentSettings(string Provider, string? ApiKey, string BaseUrl, string Model, string ProjectDir, string PromptPath);
