using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kotoba.Core;

namespace Kotoba.Infrastructure;

public static class AiHttp
{
    public static HttpClient Create(bool local = false) => new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = !local }) { Timeout = TimeSpan.FromMinutes(3) };
    public static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new HttpRequestException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "The connection has expired or was disconnected. Sign in again in Settings.",
            HttpStatusCode.Forbidden => "This account or model does not allow the request. Check your connection and model in Settings.",
            HttpStatusCode.TooManyRequests => "The provider's usage limit has been reached. Try later or select another provider.",
            _ => $"The provider returned HTTP {(int)response.StatusCode}. Check the service and selected model."
        });
    }
    public static async Task<JsonDocument> Json(HttpResponseMessage response, CancellationToken token)
    {
        EnsureSuccess(response);
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream(); var bytes = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(bytes, token)) > 0)
        {
            if (buffer.Length + read > 1024 * 1024) throw new InvalidDataException("The provider response was too large.");
            buffer.Write(bytes, 0, read);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }
}

public sealed class OllamaWordAssistant(HttpClient http) : IWordAssistant
{
    private const string Endpoint = "http://127.0.0.1:11434/api/";
    public async Task<AiModel[]> Models(CancellationToken token)
    {
        using var response = await http.GetAsync(Endpoint + "tags", HttpCompletionOption.ResponseHeadersRead, token);
        using var json = await AiHttp.Json(response, token);
        return json.RootElement.GetProperty("models").EnumerateArray().Where(m => !Remote(m)).Select(m => m.GetProperty("name").GetString())
            .OfType<string>().Where(n => !n.Contains("cloud", StringComparison.OrdinalIgnoreCase)).Select(n => new AiModel(n, n)).DistinctBy(m => m.Id).ToArray();
    }
    private static bool Remote(JsonElement model) => new[] { "remote_model", "remote_host" }.Any(p => model.TryGetProperty(p, out var value) && !string.IsNullOrEmpty(value.GetString()));
    public async Task<WordAnswer> Ask(string model, WordQuestion question, CancellationToken token)
    {
        var payload = WordAssistance.Payload(question);
        if (!(await Models(token)).Any(m => m.Id == model)) throw new ArgumentException("Choose a currently installed local Ollama model in Settings.");
        using (var show = await http.PostAsJsonAsync(Endpoint + "show", new { model }, token))
        using (var details = await AiHttp.Json(show, token))
            if (Remote(details.RootElement)) throw new ArgumentException("Choose a local Ollama model; remote/cloud models are not enabled by this provider.");
        using var schema = JsonDocument.Parse(WordAssistance.Schema);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint + "chat")
        {
            Content = JsonContent.Create(new { model, stream = false, format = schema.RootElement, messages = new[]
            {
                new { role = "system", content = WordAssistance.Instructions }, new { role = "user", content = payload }
            }, options = new { num_predict = 4096 } })
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        using var json = await AiHttp.Json(response, token);
        if (!json.RootElement.TryGetProperty("done", out var done) || !done.GetBoolean() ||
            json.RootElement.TryGetProperty("done_reason", out var reason) && reason.GetString() == "length")
            throw new InvalidDataException("The local model did not finish its answer. Try another model or a shorter question.");
        return WordAssistance.Parse(json.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "");
    }
}

public sealed class ChatGptWordAssistant(HttpClient http, Func<CancellationToken, Task<string>> accessToken) : IWordAssistant
{
    private async Task<HttpRequestMessage> Request(HttpMethod method, string path, CancellationToken token)
    {
        var request = new HttpRequestMessage(method, "https://api.openai.com/v1/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await accessToken(token));
        return request;
    }
    public async Task<AiModel[]> Models(CancellationToken token)
    {
        using var request = await Request(HttpMethod.Get, "models", token);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        using var json = await AiHttp.Json(response, token);
        return json.RootElement.GetProperty("models").EnumerateArray().Where(m => m.TryGetProperty("visibility", out var visibility) && visibility.GetString() == "list")
            .Select(m => new AiModel(m.GetProperty("slug").GetString()!, m.GetProperty("display_name").GetString()!)).ToArray();
    }
    public async Task<WordAnswer> Ask(string model, WordQuestion question, CancellationToken token)
    {
        var payload = WordAssistance.Payload(question);
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Refresh models and choose a ChatGPT model in Settings.");
        using var schema = JsonDocument.Parse(WordAssistance.Schema);
        using var request = await Request(HttpMethod.Post, "responses", token);
        request.Content = JsonContent.Create(new
        {
            model, store = false, stream = true, instructions = WordAssistance.Instructions,
            input = new[] { new { role = "user", content = payload } },
            text = new { format = new { type = "json_schema", name = "word_assistance", strict = true, schema = schema.RootElement } }
        });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        AiHttp.EnsureSuccess(response);
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        return await ReadAnswer(stream, token);
    }
    public static async Task<WordAnswer> ReadAnswer(Stream stream, CancellationToken token)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var eventData = new StringBuilder(); var answer = new StringBuilder(); var received = 0;
        while (await reader.ReadLineAsync(token) is { } line)
        {
            received += line.Length;
            if (received > 1024 * 1024) throw new InvalidDataException("The model stream was too large.");
            if (line.StartsWith("data:", StringComparison.Ordinal)) { eventData.AppendLine(line[5..].TrimStart()); continue; }
            if (line.Length != 0 || eventData.Length == 0) continue;
            var data = eventData.ToString(); eventData.Clear();
            if (data.Trim() == "[DONE]") break;
            using var json = JsonDocument.Parse(data); var root = json.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "response.output_text.delta":
                    answer.Append(root.GetProperty("delta").GetString());
                    if (answer.Length > 20000) throw new InvalidDataException("The model answer was too long.");
                    break;
                case "response.completed":
                    if (root.GetProperty("response").GetProperty("status").GetString() != "completed") throw new InvalidDataException("The model did not complete the response.");
                    return WordAssistance.Parse(answer.ToString());
                case "response.failed":
                case "error": throw new InvalidOperationException("ChatGPT could not complete the request. Your plan limit may be reached or this model may be unavailable. Check Settings or choose another provider.");
                case "response.incomplete": throw new InvalidDataException("ChatGPT returned an incomplete answer. Try a shorter question or another model.");
            }
        }
        throw new InvalidDataException("The connection ended before ChatGPT completed its answer. Please retry.");
    }
}
