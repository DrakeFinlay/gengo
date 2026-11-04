using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kotoba.Infrastructure;

public sealed record ChatGptAccount(string ClientId, string Label, bool Connected);
public sealed record ChatGptAuthorization(string State, string Nonce, string Verifier, string RedirectUri, string HostId, string? ClientId, string? IdTokenHint);

// Gengo owns these registrations. Never read Codex/ChatGPT credential files.
[SupportedOSPlatform("windows")]
public sealed class ChatGptAccounts(HttpClient http, string dataDirectory)
{
    private const string Auth = "https://auth.openai.com";
    private const string Resource = "https://api.openai.com/v1";
    private const string Scopes = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
    private readonly string directory = Path.Combine(dataDirectory, "ai");
    private sealed record Profile(string ClientId, string Subject, string Email, string AccessToken, string RefreshToken, string IdToken, string Scope, DateTimeOffset Expires);
    private sealed record State(string HostId, string? ActiveClientId, List<Profile> Profiles);

    private async Task<T> Locked<T>(Func<State, Task<(State State, T Result)>> action, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        FileStream? lease = null;
        while (lease is null)
        {
            token.ThrowIfCancellationRequested();
            try { lease = new FileStream(Path.Combine(directory, "connections.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(100, token); }
        }
        using (lease)
        {
            var path = Path.Combine(directory, "connections.bin");
            State state;
            if (File.Exists(path))
            {
                var bytes = ProtectedData.Unprotect(await File.ReadAllBytesAsync(path, token), null, DataProtectionScope.CurrentUser);
                try { state = JsonSerializer.Deserialize<State>(bytes) ?? throw new InvalidDataException("Could not read saved ChatGPT connections."); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            else state = new("urn:uuid:" + Guid.NewGuid().ToString(), null, []);
            var result = await action(state);
            var plain = JsonSerializer.SerializeToUtf8Bytes(result.State);
            byte[] encrypted;
            try { encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
            finally { CryptographicOperations.ZeroMemory(plain); }
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllBytesAsync(temporary, encrypted, token); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return result.Result;
        }
    }
    public Task<ChatGptAccount[]> List(CancellationToken token) => Locked(state => Task.FromResult((state, state.Profiles.Select(p => new ChatGptAccount(p.ClientId, p.Email + " · " + p.ClientId, p.AccessToken.Length > 0)).ToArray())), token);
    public Task<string?> Active(CancellationToken token) => Locked(state => Task.FromResult((state, state.ActiveClientId)), token);
    public Task<bool> Select(string clientId, CancellationToken token) => Locked(state =>
    {
        if (!state.Profiles.Any(p => p.ClientId == clientId)) throw new ArgumentException("Choose a saved ChatGPT connection.");
        return Task.FromResult((state with { ActiveClientId = clientId }, true));
    }, token);

    public static ChatGptAuthorization NewAuthorization(string redirectUri, string hostId, string? clientId = null, string? idTokenHint = null) =>
        new(Random(), Random(), Random(), redirectUri, hostId, clientId, idTokenHint);
    private static string Random() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    public static string AuthorizationUrl(ChatGptAuthorization attempt)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = attempt.ClientId ?? "dynamic_agent_client", ["ext_agent_host_id"] = attempt.HostId,
            ["response_type"] = "code", ["redirect_uri"] = attempt.RedirectUri, ["scope"] = Scopes,
            ["resource"] = Resource, ["state"] = attempt.State, ["nonce"] = attempt.Nonce,
            ["code_challenge_method"] = "S256", ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(attempt.Verifier)))
        };
        if (attempt.ClientId is null) query["agent_name_hint"] = "Gengo";
        if (attempt.IdTokenHint is { Length: > 0 }) query["id_token_hint"] = attempt.IdTokenHint;
        return Auth + "/api/accounts/authorize?" + string.Join("&", query.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));
    }
    public static Dictionary<string, string> Query(Uri callback)
    {
        var result = new Dictionary<string, string>();
        foreach (var item in callback.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', 2);
            if (!result.TryAdd(Uri.UnescapeDataString(parts[0]), parts.Length == 2 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : ""))
                throw new InvalidDataException("The sign-in callback contained duplicate fields.");
        }
        return result;
    }
    public static (string Code, string ClientId) ValidateCallback(ChatGptAuthorization attempt, Uri callback)
    {
        if (callback.GetLeftPart(UriPartial.Path) != attempt.RedirectUri) throw new InvalidDataException("The sign-in callback address did not match.");
        var query = Query(callback);
        if (!query.TryGetValue("state", out var state) || state != attempt.State) throw new InvalidDataException("The sign-in state did not match. Start sign-in again.");
        if (query.ContainsKey("error")) throw new InvalidOperationException("ChatGPT sign-in was declined or could not be completed.");
        var clientId = query.GetValueOrDefault("client_id") ?? attempt.ClientId;
        if (string.IsNullOrWhiteSpace(clientId) || clientId == "dynamic_agent_client" || attempt.ClientId is not null && clientId != attempt.ClientId)
            throw new InvalidDataException("ChatGPT did not return the expected registered client.");
        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code)) throw new InvalidDataException("ChatGPT did not return an authorization code.");
        return (code, clientId);
    }
    public async Task SignIn(string? clientId, Action<string> openBrowser, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(4));
        var ct = timeout.Token;
        using var listener = new HttpListener();
        // Bind an available IPv4 loopback port before handing its callback URI to the browser.
        string redirectUri = "";
        for (var retry = 0; retry < 10; retry++)
        {
            var portFinder = new TcpListener(IPAddress.Loopback, 0); portFinder.Start(); var port = ((IPEndPoint)portFinder.LocalEndpoint).Port; portFinder.Stop();
            listener.Prefixes.Clear(); listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try { listener.Start(); redirectUri = $"http://127.0.0.1:{port}/auth/callback"; break; }
            catch (HttpListenerException) when (retry < 9) { }
        }
        var attempt = await Locked(state =>
        {
            var profile = clientId is null ? null : state.Profiles.Single(p => p.ClientId == clientId);
            return Task.FromResult((state, NewAuthorization(redirectUri, state.HostId, profile?.ClientId, profile?.IdToken)));
        }, ct);
        openBrowser(AuthorizationUrl(attempt));
        while (true)
        {
            var context = await listener.GetContextAsync().WaitAsync(ct);
            var callback = context.Request.Url!;
            var validState = callback.AbsolutePath == "/auth/callback" && Query(callback).GetValueOrDefault("state") == attempt.State;
            var bytes = Encoding.UTF8.GetBytes(validState ? "Gengo received the sign-in response. Return to Gengo to see the connection result." : "This is not the pending Gengo sign-in.");
            context.Response.StatusCode = validState ? 200 : 400; context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.OutputStream.WriteAsync(bytes, ct); context.Response.Close();
            if (!validState) continue;
            await CompleteSignIn(attempt, callback, ct); return;
        }
    }
    public async Task CompleteSignIn(ChatGptAuthorization attempt, Uri callback, CancellationToken token)
    {
        var result = ValidateCallback(attempt, callback);
        // Retain an issued registration even if code exchange or identity validation fails.
        // This is not a connected account until validation and token storage succeed.
        await Locked(state =>
        {
            if (!state.Profiles.Any(p => p.ClientId == result.ClientId))
                state.Profiles.Add(new(result.ClientId, "", "Registration awaiting sign-in", "", "", "", "", DateTimeOffset.MinValue));
            return Task.FromResult((state with { ActiveClientId = state.ActiveClientId ?? result.ClientId }, true));
        }, token);
        using var response = await http.PostAsync(Auth + "/api/accounts/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = result.ClientId, ["code"] = result.Code,
            ["code_verifier"] = attempt.Verifier, ["redirect_uri"] = attempt.RedirectUri, ["resource"] = Resource
        }), token);
        using var json = await AiHttp.Json(response, token);
        var root = json.RootElement; var idToken = root.GetProperty("id_token").GetString()!;
        var identity = await ValidateIdentity(idToken, result.ClientId, attempt.Nonce, token);
        var profile = ReadProfile(root, result.ClientId, identity.Subject, identity.Email, null);
        await Locked(state =>
        {
            var previous = state.Profiles.Find(p => p.ClientId == result.ClientId);
            if (previous is { Subject.Length: > 0 } && previous.Subject != profile.Subject) throw new InvalidDataException("The sign-in identity did not match the saved account.");
            state.Profiles.RemoveAll(p => p.ClientId == profile.ClientId); state.Profiles.Add(profile);
            return Task.FromResult((state with { ActiveClientId = profile.ClientId }, true));
        }, token);
    }
    public async Task<(string Subject, string Email)> ValidateIdentity(string idToken, string clientId, string? nonce, CancellationToken token)
    {
        using var keysResponse = await http.GetAsync(Auth + "/.well-known/jwks.json", HttpCompletionOption.ResponseHeadersRead, token);
        using var keys = await AiHttp.Json(keysResponse, token);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = Auth, ValidateAudience = true, ValidAudience = clientId,
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256], IssuerSigningKeys = new JsonWebKeySet(keys.RootElement.GetRawText()).GetSigningKeys(), ClockSkew = TimeSpan.FromSeconds(30)
        });
        if (!validation.IsValid) throw new InvalidDataException("ChatGPT identity validation failed. Start sign-in again.");
        var claims = validation.ClaimsIdentity;
        var subject = claims.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject) || nonce is not null && claims.FindFirst("nonce")?.Value != nonce) throw new InvalidDataException("ChatGPT identity or nonce did not match.");
        var authorizedParty = claims.FindFirst("azp")?.Value;
        if (authorizedParty is not null && authorizedParty != clientId || validation.SecurityToken is JsonWebToken jwt && jwt.Audiences.Count() > 1 && authorizedParty != clientId)
            throw new InvalidDataException("ChatGPT authorized-party validation failed.");
        return (subject, claims.FindFirst("email")?.Value ?? subject);
    }
    private static Profile ReadProfile(JsonElement root, string clientId, string subject, string email, Profile? old)
    {
        var scope = root.TryGetProperty("scope", out var granted) ? granted.GetString() ?? "" : old?.Scope ?? "";
        if (!scope.Split(' ').Contains("chatgpt.tokens.use.direct")) throw new InvalidOperationException("ChatGPT plan usage was not granted. Sign in again and allow plan usage.");
        if (!root.TryGetProperty("token_type", out var type) || !string.Equals(type.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ChatGPT did not issue a bearer token.");
        var access = root.GetProperty("access_token").GetString();
        if (string.IsNullOrWhiteSpace(access)) throw new InvalidDataException("ChatGPT did not issue an access token.");
        return new(clientId, subject, email, access,
            root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : old?.RefreshToken ?? "",
            root.TryGetProperty("id_token", out var id) ? id.GetString() ?? "" : old?.IdToken ?? "", scope,
            DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32()));
    }
    public Task<string> AccessToken(CancellationToken token) => Locked(async state =>
    {
        var profile = state.Profiles.FirstOrDefault(p => p.ClientId == state.ActiveClientId);
        if (profile is null || profile.AccessToken.Length == 0) throw new InvalidOperationException("Continue with ChatGPT in Settings before asking a question.");
        if (profile.Expires > DateTimeOffset.UtcNow.AddMinutes(1)) return (state, profile.AccessToken);
        if (profile.RefreshToken.Length == 0) throw new InvalidOperationException("Sign in again to renew your ChatGPT connection.");
        using var response = await http.PostAsync(Auth + "/api/accounts/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = profile.ClientId, ["refresh_token"] = profile.RefreshToken, ["resource"] = Resource
        }), token);
        using var json = await AiHttp.Json(response, token);
        if (json.RootElement.TryGetProperty("id_token", out var newIdentity))
        {
            var identity = await ValidateIdentity(newIdentity.GetString()!, profile.ClientId, null, token);
            if (identity.Subject != profile.Subject) throw new InvalidDataException("The renewed ChatGPT identity did not match.");
        }
        var renewed = ReadProfile(json.RootElement, profile.ClientId, profile.Subject, profile.Email, profile);
        state.Profiles.Remove(profile); state.Profiles.Add(renewed);
        return (state, renewed.AccessToken);
    }, token);

    public Task<bool> SignOut(CancellationToken token) => Locked(async state =>
    {
        var profile = state.Profiles.FirstOrDefault(p => p.ClientId == state.ActiveClientId);
        if (profile is null) return (state, true);
        var revoked = profile.RefreshToken.Length == 0;
        string? endpoint = null;
        if (!revoked)
        {
            try
            {
                using var discoveryTimeout = CancellationTokenSource.CreateLinkedTokenSource(token); discoveryTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var discovery = await http.GetAsync(Auth + "/.well-known/openid-configuration", HttpCompletionOption.ResponseHeadersRead, discoveryTimeout.Token);
                using var configuration = await AiHttp.Json(discovery, discoveryTimeout.Token);
                var uri = new Uri(configuration.RootElement.GetProperty("revocation_endpoint").GetString()!);
                if (uri.Scheme == "https" && uri.Host == "auth.openai.com" && uri.IsDefaultPort) endpoint = uri.AbsoluteUri;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException) { token.ThrowIfCancellationRequested(); }
        }
        for (var attempt = 0; !revoked && endpoint is not null && attempt < 2; attempt++)
        {
            try
            {
                using var revokeTimeout = CancellationTokenSource.CreateLinkedTokenSource(token); revokeTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var response = await http.PostAsync(endpoint, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = profile.RefreshToken, ["token_type_hint"] = "refresh_token", ["client_id"] = profile.ClientId
                }), revokeTimeout.Token);
                revoked = response.StatusCode == HttpStatusCode.OK;
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            if (!revoked && attempt == 0) await Task.Delay(500, token);
        }
        state.Profiles.Remove(profile); state.Profiles.Add(profile with { AccessToken = "", RefreshToken = "", IdToken = "", Scope = "", Expires = DateTimeOffset.MinValue });
        return (state, revoked);
    }, token);
}
