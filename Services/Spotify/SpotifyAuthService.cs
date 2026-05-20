using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SyncDeck.Services.Spotify;

/// <summary>
/// Handles Spotify OAuth 2.0 with PKCE — the recommended flow for public desktop clients
/// that cannot securely store a client secret.
///
/// SETUP REQUIRED (Milestone 2):
///   1. Go to https://developer.spotify.com/dashboard and create an app.
///   2. Add  http://localhost:5543/callback  as a Redirect URI.
///   3. Set the environment variable SPOTIFY_CLIENT_ID  (or update appsettings.json).
///   4. The user must have Spotify Premium to control playback via the Web API.
/// </summary>
public class SpotifyAuthService
{
    private const string CallbackUri = "http://localhost:5543/callback";
    private const string Scopes      =
        "user-read-playback-state user-modify-playback-state " +
        "user-read-currently-playing playlist-read-private playlist-read-collaborative";

    private readonly string     _clientId;
    private readonly HttpClient _http = new();

    public SpotifyAuthService(string clientId) => _clientId = clientId;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Opens the browser, waits for callback, exchanges code for tokens.</summary>
    public async Task<SpotifyTokenResponse?> AuthorizeAsync(CancellationToken ct = default)
    {
        var (verifier, challenge) = GeneratePkce();
        var state = GenerateState();

        var authUrl = BuildAuthUrl(challenge, state);
        OpenBrowser(authUrl);

        var code = await WaitForCallbackAsync(state, ct);
        if (code is null) return null;

        return await ExchangeCodeAsync(code, verifier);
    }

    /// <summary>Uses the refresh token to silently obtain a new access token.</summary>
    public async Task<SpotifyTokenResponse?> RefreshAsync(string refreshToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"]     = _clientId,
        };
        return await PostTokenAsync(form);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private string BuildAuthUrl(string challenge, string state)
    {
        var q = HttpUtility.ParseQueryString(string.Empty);
        q["client_id"]             = _clientId;
        q["response_type"]         = "code";
        q["redirect_uri"]          = CallbackUri;
        q["code_challenge_method"] = "S256";
        q["code_challenge"]        = challenge;
        q["state"]                 = state;
        q["scope"]                 = Scopes;
        return $"https://accounts.spotify.com/authorize?{q}";
    }

    private async Task<string?> WaitForCallbackAsync(string expectedState, CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5543/");
        listener.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var ctx  = await listener.GetContextAsync().WaitAsync(cts.Token);
        var req  = ctx.Request;
        var q    = HttpUtility.ParseQueryString(req.Url?.Query ?? "");

        const string html =
            "<html><body style='font-family:sans-serif;background:#111;color:#3CA0FF;padding:40px'>" +
            "<h2>✓ SyncDeck: Authorization complete.</h2><p>You can close this tab.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes, cts.Token);
        ctx.Response.Close();

        listener.Stop();

        if (q["error"] is { } err)  throw new Exception($"Spotify auth error: {err}");
        if (q["state"] != expectedState) throw new Exception("OAuth state mismatch — possible CSRF.");
        return q["code"];
    }

    private async Task<SpotifyTokenResponse?> ExchangeCodeAsync(string code, string verifier)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = CallbackUri,
            ["client_id"]     = _clientId,
            ["code_verifier"] = verifier,
        };
        return await PostTokenAsync(form);
    }

    private async Task<SpotifyTokenResponse?> PostTokenAsync(Dictionary<string, string> form)
    {
        var res = await _http.PostAsync(
            "https://accounts.spotify.com/api/token",
            new FormUrlEncodedContent(form));

        if (!res.IsSuccessStatusCode) return null;
        return JsonSerializer.Deserialize<SpotifyTokenResponse>(
            await res.Content.ReadAsStringAsync());
    }

    // ── PKCE helpers ──────────────────────────────────────────────────────────

    private static (string verifier, string challenge) GeneratePkce()
    {
        Span<byte> buf = stackalloc byte[32];
        RandomNumberGenerator.Fill(buf);

        var verifier  = ToBase64Url(buf.ToArray());
        var hash      = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = ToBase64Url(hash);
        return (verifier, challenge);
    }

    private static string ToBase64Url(byte[] b)
        => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string GenerateState()
    {
        var b = new byte[16];
        RandomNumberGenerator.Fill(b);
        return Convert.ToHexString(b);
    }

    private static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
        else
            Process.Start("xdg-open", url);
    }
}
