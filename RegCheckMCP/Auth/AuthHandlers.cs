using RegCheckMcp.Resources;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RegCheckMcp.Auth;

public static class AuthHandlers
{
    private static readonly string LoginHtml = EmbeddedResource.LoadText("login.html");

  
    // GET /authorize
    // Called by the MCP client to start the OAuth flow.
    // Validates required PKCE params, mints a session token, redirects to /login.
    public static IResult Authorize(HttpContext ctx)
    {

        var q = ctx.Request.Query;
        var responseType = q["response_type"].ToString();
        var clientId = q["client_id"].ToString();
        var redirectUri = q["redirect_uri"].ToString();
        var codeChallenge = q["code_challenge"].ToString();
        var codeChallengeMethod = q["code_challenge_method"].ToString();
        var state = q["state"].ToString();
        if (responseType != "code")
            return Results.BadRequest("unsupported_response_type");
        if (string.IsNullOrEmpty(clientId))
            return Results.BadRequest("missing client_id");
        if (string.IsNullOrEmpty(redirectUri))
            return Results.BadRequest("missing redirect_uri");
        if (string.IsNullOrEmpty(codeChallenge))
            return Results.BadRequest("missing code_challenge");
        if (codeChallengeMethod != "S256")
            return Results.BadRequest("only S256 code_challenge_method is supported");

        // TODO: validate clientId and redirectUri against a known-clients list

        var session = SessionToken.NewSession(
            codeChallenge, codeChallengeMethod,
            clientId, redirectUri, state ?? "");

        var token = SessionToken.Create(session);
        return Results.Redirect($"/login?session_token={Uri.EscapeDataString(token)}");
    }

    // GET /login  — serves the login page with session_token embedded
    public static IResult LoginPage(HttpContext ctx)
    {
        var sessionToken = ctx.Request.Query["session_token"].ToString();
        var error = ctx.Request.Query["error"].ToString();
        if (string.IsNullOrEmpty(sessionToken))
            return Results.BadRequest("missing session_token");

        // Verify the token is valid before showing the page — no point serving it if it's already expired
        if (SessionToken.Verify(sessionToken) is null)
            return Results.Content(LoginHtml.Replace("{{SESSION_TOKEN}}", "")
                                            .Replace("{{ERROR}}", "expired"), "text/html");

        var html = LoginHtml
            .Replace("{{SESSION_TOKEN}}", sessionToken)
            .Replace("{{ERROR}}", error ?? "");

        return Results.Content(html, "text/html");
    }

    // POST /login — validates credentials, issues auth code, redirects back to client
    public static async Task LoginPost(HttpContext ctx)
    {
        var form = await ctx.Request.ReadFormAsync();
        var username = form["username"].ToString().Trim();
        var password = form["password"].ToString();
        var sessionToken = form["session_token"].ToString();

        Console.WriteLine($"[LOGIN] username='{username}' session_token_length={sessionToken.Length}");

        if (string.IsNullOrEmpty(sessionToken))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync("missing session_token");
            return;
        }

        var session = SessionToken.Verify(sessionToken);
        if (session is null)
        {
            Console.WriteLine("[LOGIN] session token invalid or expired");
            ctx.Response.Redirect($"/login?session_token={Uri.EscapeDataString(sessionToken)}&error=expired");
            return;
        }

        Console.WriteLine("[LOGIN] session token OK, validating credentials...");

        bool credentialsOk;
        try { credentialsOk = await ValidateCredentials(username, password); }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOGIN] ValidateCredentials threw: {ex.Message}");
            credentialsOk = false;
        }

        Console.WriteLine($"[LOGIN] credentials valid={credentialsOk}");

        if (!credentialsOk)
        {
            ctx.Response.Redirect($"/login?session_token={Uri.EscapeDataString(sessionToken)}&error=invalid");
            return;
        }

        var code = AuthCode.Create(AuthCode.NewPayload(username, session));
        Console.WriteLine($"[LOGIN] auth code issued, redirecting to {session.RedirectUri}");

        var redirectUri = new UriBuilder(session.RedirectUri);
        var qs = System.Web.HttpUtility.ParseQueryString(redirectUri.Query);
        qs["code"] = code;
        qs["state"] = session.State;
        redirectUri.Query = qs.ToString();

        var finalUrl = redirectUri.ToString();
        Console.WriteLine($"[LOGIN] final redirect URL: {finalUrl}");
        ctx.Response.Redirect(finalUrl);
    }

    // POST /token — client exchanges auth code + code_verifier for an access token
    public static async Task Token(HttpContext ctx)
    {
        var form = await ctx.Request.ReadFormAsync();
        var grantType = form["grant_type"].ToString();
        var code = form["code"].ToString();
        var codeVerifier = form["code_verifier"].ToString();
        var clientId = form["client_id"].ToString();

        ctx.Response.ContentType = "application/json";

        async Task WriteError(int status, object body)
        {
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(body));
        }

        if (grantType != "authorization_code")
        {
            await WriteError(400, new { error = "unsupported_grant_type" });
            return;
        }

        var payload = AuthCode.Verify(code);
        if (payload is null)
        {
            await WriteError(400, new { error = "invalid_grant" });
            return;
        }

        if (payload.ClientId != clientId)
        {
            await WriteError(400, new { error = "invalid_client" });
            return;
        }

        if (!VerifyPkce(codeVerifier, payload.CodeChallenge))
        {
            await WriteError(400, new { error = "invalid_grant", error_description = "PKCE verification failed" });
            return;
        }

        var accessToken = GenerateAccessToken(payload.Username, payload.ClientId);

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = 3600
        }));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool VerifyPkce(string codeVerifier, string codeChallenge)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var derived = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(derived),
            Encoding.ASCII.GetBytes(codeChallenge));
    }

    private static string GenerateAccessToken(string username, string clientId)
    {
        // Minimal signed token: base64(payload).hmac
        // Replace with proper JWT (e.g. System.IdentityModel.Tokens.Jwt) if you need expiry claims etc.
        var payload = $"{username}:{clientId}:{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}";
        var key = Encoding.UTF8.GetBytes(Secrets.Load().SessionSigningKey);
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var sigB64 = Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{payloadB64}.{sigB64}";
    }

    private static readonly IHttpClientFactory? _httpClientFactory;

    // Add this constructor or inject via DI — see note below
    private static async Task<bool> ValidateCredentials(string username, string password)
    {
        using var http = new HttpClient();

        try
        {
            var url = $"https://www.regcheck.org.uk/ajax/getcredits.aspx?username={Uri.EscapeDataString(username)}";
            var response = await http.GetStringAsync(url);

            // Returns "0" or error text = invalid, any positive number = valid
            if (int.TryParse(response.Trim(), out var credits))
                return credits > 0;

            return false;
        }
        catch
        {
            return false;
        }
    }
}