using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ScrumAgent.ConsoleApp;

public sealed class OpenAiResponsesClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public OpenAiResponsesClient(HttpClient httpClient, string provider, string? apiKey, string baseUrl, string model)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(NormalizeBaseUrl(baseUrl));
        _httpClient.Timeout = TimeSpan.FromMinutes(6);
        _model = NormalizeModel(model, provider, _httpClient.BaseAddress);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        Console.WriteLine(_httpClient.DefaultRequestHeaders.Authorization?.Scheme);
        Console.WriteLine(string.IsNullOrWhiteSpace(apiKey));
        Console.WriteLine(apiKey?.StartsWith("sk-or-v1-"));
    }

    public async Task<string> CompleteAsync(IReadOnlyList<ConversationItem> messages, CancellationToken cancellationToken)
    {
        var chatMessages = messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToArray();

        var payload = new
        {
            model = _model,
            format = "json",    // Ollama
            stream = false,    // Ollama
            messages = chatMessages,
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LLM API error {(int)response.StatusCode}: {responseText}");
        }

        return ExtractText(responseText);
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : baseUrl + "/";
    }

    private static string NormalizeModel(string model, string provider, Uri baseAddress)
    {
        const string openRouterProviderPrefix = "openrouter/";
        const string ollamaProviderPrefix = "ollama/";

        if ((provider == "openrouter" || baseAddress.Host.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase))
            && model.StartsWith(openRouterProviderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return model[openRouterProviderPrefix.Length..];
        }

        if (provider == "ollama" && model.StartsWith(ollamaProviderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return model[ollamaProviderPrefix.Length..];
        }

        return model;
    }

    private static string ExtractText(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString() ?? string.Empty;
                }
            }
        }

        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        return text.GetString() ?? string.Empty;
                }
            }
        }

        throw new InvalidOperationException("Could not extract text from LLM response.");
    }
}