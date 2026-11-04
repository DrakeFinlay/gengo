using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kotoba.Core;
using Kotoba.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

internal static class WordAssistanceChecks
{
    private static readonly WordAnswer Answer = new("ギルガメッシュ", "ぎるがめっしゅ", "Gilgamesh", "Likely a proper name; context is limited.", ["A fictional namesake"], "Cannot establish which portrayal from a name alone.", "Names");
    private static string AnswerJson => JsonSerializer.Serialize(Answer, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    public static async Task Run(Action<bool, string> check)
    {
        var question = new WordQuestion("ギルガメッシュ", "ギルガメッシュが来た。", null, "Is this a name?", []);
        var parsed = WordAssistance.Parse(AnswerJson);
        check(parsed.Expression == Answer.Expression && parsed.Reading == Answer.Reading && parsed.Alternatives.SequenceEqual(Answer.Alternatives) && parsed.Uncertainty == Answer.Uncertainty, "AI structured answer retains reading, alternatives and uncertainty");
        check(WordAssistance.Payload(question).Contains("ギルガメッシュ") && WordAssistance.Payload(question).Contains("selected_expression") && WordAssistance.Instructions.Contains("never instructions"), "AI prompt keeps Japanese readable and separates passage data from instructions");
        var repeated = "床" + new string('a', 15000) + "床に置く。";
        check(WordAssistance.ContextWindow(repeated, "床", 15001).Contains("床に置く。"), "AI context window follows the selected occurrence rather than the first identical word");
        void Reject(Action action, string label) { try { action(); check(false, label); } catch (Exception ex) when (ex is ArgumentException or InvalidDataException or JsonException) { check(true, label); } }
        Reject(() => WordAssistance.Parse("{}"), "AI answer rejects missing structured fields");
        Reject(() => WordAssistance.Parse(AnswerJson.Replace("Names", "Invented category")), "AI answer rejects invalid personal category");
        Reject(() => WordAssistance.Payload(question with { Context = new string('x', 12001) }), "AI context bounds apply before requests");
        Reject(() => WordAssistance.Payload(question with { Conversation = Enumerable.Repeat(new AiExchange("q", "a"), 7).ToArray() }), "AI follow-up context is bounded");

        var requests = new List<(Uri Uri, string Body)>();
        using var localHttp = new HttpClient(new Handler(async (r, ct) =>
        {
            var body = r.Content is null ? "" : await r.Content.ReadAsStringAsync(ct); requests.Add((r.RequestUri!, body));
            return Json(r.RequestUri!.AbsolutePath switch
            {
                "/api/tags" => "{\"models\":[{\"name\":\"qwen2.5:7b-instruct\"},{\"name\":\"remote:cloud\",\"remote_model\":\"remote\"}]}",
                "/api/show" => "{}",
                "/api/chat" => JsonSerializer.Serialize(new { done = true, done_reason = "stop", message = new { content = AnswerJson } }),
                _ => throw new Exception("Unexpected Ollama route")
            });
        }));
        var local = new OllamaWordAssistant(localHttp);
        check((await local.Models(default)).Select(m => m.Id).SequenceEqual(["qwen2.5:7b-instruct"]), "Ollama picker discovers current installed local models and excludes cloud models");
        var localAnswer = await local.Ask("qwen2.5:7b-instruct", question, default);
        using var localBody = JsonDocument.Parse(requests.Last().Body);
        check(localAnswer.Meaning == "Gilgamesh" && !localBody.RootElement.GetProperty("stream").GetBoolean() && localBody.RootElement.GetProperty("format").ValueKind == JsonValueKind.Object && requests.All(r => r.Uri.Host == "127.0.0.1"), "Ollama uses only loopback with structured output and no tools/downloads");
        var calls = requests.Count;
        try { await local.Ask("removed-model", question, default); check(false, "removed Ollama model is rejected before inference"); }
        catch (ArgumentException) { check(requests.Count == calls + 1, "removed Ollama model is rejected before inference"); }

        static string Event(object value) => "data: " + JsonSerializer.Serialize(value) + "\n\n";
        var delta = Event(new { type = "response.output_text.delta", delta = AnswerJson });
        var completed = Event(new { type = "response.completed", response = new { status = "completed" } });
        async Task RejectStream(string stream, string label)
        {
            try { await ChatGptWordAssistant.ReadAnswer(new MemoryStream(Encoding.UTF8.GetBytes(stream)), default); check(false, label); }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException) { check(true, label); }
        }
        check((await ChatGptWordAssistant.ReadAnswer(new MemoryStream(Encoding.UTF8.GetBytes(delta + completed)), default)).Meaning == "Gilgamesh", "ChatGPT SSE answer is accepted only after completed inference");
        await RejectStream(delta, "interrupted ChatGPT stream never becomes an answer");
        await RejectStream(delta + Event(new { type = "response.failed", response = new { error = new { code = "subscription_sharing_usage_limit_exceeded" } } }), "late ChatGPT plan-limit failure discards partial output");
        await RejectStream(delta + Event(new { type = "response.incomplete" }), "incomplete ChatGPT response is rejected");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { await ChatGptWordAssistant.ReadAnswer(new MemoryStream(Encoding.UTF8.GetBytes(delta + completed)), canceled.Token); check(false, "AI stream respects cancellation"); }
        catch (OperationCanceledException) { check(true, "AI stream respects cancellation"); }

        string? cloudBody = null;
        using var cloudHttp = new HttpClient(new Handler(async (r, ct) =>
        {
            check(r.RequestUri!.Host == "api.openai.com" && r.Headers.Authorization?.Parameter == "synthetic-access", "ChatGPT sends only its own bearer credential to the public API");
            if (r.Method == HttpMethod.Get) return Json("{\"models\":[{\"slug\":\"model-a\",\"display_name\":\"Model A\",\"visibility\":\"list\"},{\"slug\":\"hidden\",\"display_name\":\"Hidden\",\"visibility\":\"hide\"}]}");
            cloudBody = await r.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(delta + completed, Encoding.UTF8, "text/event-stream") };
        }));
        var cloud = new ChatGptWordAssistant(cloudHttp, _ => Task.FromResult("synthetic-access"));
        check((await cloud.Models(default)).Select(m => m.Id).SequenceEqual(["model-a"]), "ChatGPT model selector uses the account catalog and server visibility");
        await cloud.Ask("model-a", question, default);
        using var bodyJson = JsonDocument.Parse(cloudBody!); var bodyRoot = bodyJson.RootElement;
        check(bodyRoot.GetProperty("store").GetBoolean() == false && bodyRoot.GetProperty("stream").GetBoolean() && bodyRoot.GetProperty("input").ValueKind == JsonValueKind.Array && !bodyRoot.TryGetProperty("max_output_tokens", out _) && !bodyRoot.TryGetProperty("tools", out _), "ChatGPT respects plan-route requirements and cannot execute tools");
        if (OperatingSystem.IsWindows()) await Authentication(check);
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static async Task Authentication(Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "gengo-ai-check-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var rsa = RSA.Create(2048); var key = new RsaSecurityKey(rsa) { KeyId = "synthetic-key" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = key.KeyId });
        var attempt = ChatGptAccounts.NewAuthorization("http://127.0.0.1:1455/auth/callback", "urn:uuid:" + Guid.NewGuid());
        string Signed(string audience, string nonce, string issuer = "https://auth.openai.com", bool expired = false) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer, Audience = audience, Expires = DateTime.UtcNow.AddHours(expired ? -1 : 1),
            IssuedAt = DateTime.UtcNow.AddHours(-2), NotBefore = DateTime.UtcNow.AddHours(-2),
            Claims = new Dictionary<string, object> { ["sub"] = "synthetic-subject", ["email"] = "fixture@example.invalid", ["nonce"] = nonce },
            SigningCredentials = new(key, SecurityAlgorithms.RsaSha256)
        });
        var signed = Signed("issued-client", attempt.Nonce); var tokenPosts = new List<string>(); var grantScope = "openid chatgpt.tokens.use.direct"; var failRevocation = false;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("jwks.json")) return Json(JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", kid = key.KeyId, use = "sig", alg = "RS256", n = jwk.N, e = jwk.E } } }));
            if (request.RequestUri.AbsolutePath.EndsWith("openid-configuration")) return Json("{\"revocation_endpoint\":\"https://auth.openai.com/api/accounts/oauth/revoke\"}");
            if (request.RequestUri.AbsolutePath.EndsWith("revoke")) return new(failRevocation ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
            var body = await request.Content!.ReadAsStringAsync(ct); tokenPosts.Add(body);
            return Json(JsonSerializer.Serialize(new { token_type = "Bearer", access_token = body.Contains("refresh_token") ? "refreshed-access" : "synthetic-access", refresh_token = "synthetic-refresh", id_token = signed, scope = grantScope, expires_in = body.Contains("grant_type=refresh_token") ? 3600 : 0 }));
        }));
        try
        {
            var accounts = new ChatGptAccounts(http, root);
            var url = new Uri(ChatGptAccounts.AuthorizationUrl(attempt)); var query = ChatGptAccounts.Query(url);
            check(query["client_id"] == "dynamic_agent_client" && query["agent_name_hint"] == "Gengo" && query["code_challenge"] != attempt.Verifier && query["scope"].Contains("chatgpt.tokens.use.direct"), "ChatGPT registration uses fresh PKCE/state/nonce and explicit plan scope");
            void RejectCallback(string queryString, string label)
            {
                try { ChatGptAccounts.ValidateCallback(attempt, new Uri(attempt.RedirectUri + queryString)); check(false, label); }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException) { check(true, label); }
            }
            RejectCallback("?state=wrong&code=x&client_id=issued-client", "OAuth rejects mismatched state before code exchange");
            RejectCallback("?state=" + attempt.State + "&code=x", "OAuth rejects new registration without an issued client ID");
            RejectCallback("?state=" + attempt.State + "&error=access_denied", "OAuth handles denied consent without code exchange");
            await accounts.CompleteSignIn(attempt, new Uri(attempt.RedirectUri + "?state=" + attempt.State + "&code=synthetic-code&client_id=issued-client"), default);
            var stored = await File.ReadAllBytesAsync(Path.Combine(root, "ai", "connections.bin"));
            check(!Encoding.UTF8.GetString(stored).Contains("synthetic-access") && (await accounts.List(default)).Single().Connected, "ChatGPT tokens persist only in Windows-protected storage separate from personal SQLite");
            check(tokenPosts[0].Contains("client_id=issued-client") && !tokenPosts[0].Contains("dynamic_agent_client"), "code exchange uses the callback-issued client and exact redirect URI");
            var renewed = await Task.WhenAll(accounts.AccessToken(default), new ChatGptAccounts(http, root).AccessToken(default));
            check(renewed.All(t => t == "refreshed-access") && tokenPosts.Count(p => p.Contains("grant_type=refresh_token")) == 1, "concurrent ChatGPT account instances refresh only once using the issued client and rotating token");
            var forgedParts = signed.Split('.'); var signature = Base64UrlEncoder.DecodeBytes(forgedParts[2]); signature[0] ^= 1;
            var forged = forgedParts[0] + "." + forgedParts[1] + "." + Base64UrlEncoder.Encode(signature);
            foreach (var invalid in new[] { Signed("wrong-client", attempt.Nonce), Signed("issued-client", "wrong-nonce"), Signed("issued-client", attempt.Nonce, "https://wrong.invalid"), Signed("issued-client", attempt.Nonce, expired: true), forged })
            {
                try { await accounts.ValidateIdentity(invalid, "issued-client", attempt.Nonce, default); check(false, "OIDC rejects incorrect audience, nonce, issuer, expiry or signature"); }
                catch (InvalidDataException) { check(true, "OIDC rejects incorrect audience, nonce, issuer, expiry or signature"); }
            }
            grantScope = "openid";
            try { await accounts.CompleteSignIn(attempt, new Uri(attempt.RedirectUri + "?state=" + attempt.State + "&code=synthetic-code&client_id=issued-client"), default); check(false, "missing ChatGPT plan scope cannot replace an existing connection"); }
            catch (InvalidOperationException) { check((await accounts.AccessToken(default)) == "refreshed-access", "missing ChatGPT plan scope cannot replace an existing connection"); }
            grantScope = "openid chatgpt.tokens.use.direct";
            check(await accounts.SignOut(default) && !(await accounts.List(default)).Single().Connected, "ChatGPT sign-out revokes the session and clears credentials while retaining registration");
            try { await accounts.AccessToken(default); check(false, "signed-out ChatGPT cannot infer"); }
            catch (InvalidOperationException) { check(true, "signed-out ChatGPT cannot infer"); }
            var another = ChatGptAccounts.NewAuthorization(attempt.RedirectUri, attempt.HostId); signed = Signed("another-issued-client", another.Nonce);
            await accounts.CompleteSignIn(another, new Uri(another.RedirectUri + "?state=" + another.State + "&code=second-code&client_id=another-issued-client"), default);
            check((await accounts.List(default)).Length == 2 && await accounts.Active(default) == "another-issued-client", "same-email ChatGPT registrations remain separate and switch only after validated sign-in");
            var returning = ChatGptAccounts.NewAuthorization(attempt.RedirectUri, attempt.HostId, "issued-client");
            var returningQuery = ChatGptAccounts.Query(new Uri(ChatGptAccounts.AuthorizationUrl(returning)));
            check(returningQuery["client_id"] == "issued-client" && !returningQuery.ContainsKey("agent_name_hint"), "returning ChatGPT sign-in reuses its registration instead of dynamically registering again");
            Task? callback = null;
            using var signInTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await accounts.SignIn(null, url =>
            {
                var parameters = ChatGptAccounts.Query(new Uri(url)); signed = Signed("listener-issued-client", parameters["nonce"]);
                callback = Task.Run(async () =>
                {
                    using var browser = AiHttp.Create(local: true);
                    using var response = await browser.GetAsync(parameters["redirect_uri"] + "?state=" + parameters["state"] + "&code=loopback-code&client_id=listener-issued-client", signInTimeout.Token);
                    check(response.IsSuccessStatusCode, "OAuth loopback listener accepts the synthetic browser callback");
                });
            }, signInTimeout.Token);
            await callback!;
            check((await accounts.List(default)).Any(p => p.ClientId == "listener-issued-client" && p.Connected), "actual local callback completes registration and identity validation without a browser or live account");
            await accounts.SignOut(default); await accounts.Select("another-issued-client", default);
            failRevocation = true;
            check(!await accounts.SignOut(default) && (await accounts.List(default)).All(p => !p.Connected), "failed remote revocation is reported while local ChatGPT tokens are still cleared");
        }
        finally { Directory.Delete(root, true); }
    }
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
