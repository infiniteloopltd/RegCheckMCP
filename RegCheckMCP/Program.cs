

using System.Text.Json;
using ModelContextProtocol.Protocol;
using RegCheckMcp.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("regcheck");
builder.Services.AddCors(options =>
{
    options.AddPolicy("McpInspector", policy =>
    {
        policy
            .WithOrigins(
                "https://inspector.modelcontextprotocol.io",
                "http://localhost:6274",   // MCP Inspector default local port
                "http://localhost:5173"    // Vite dev server if running locally
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddMcpServer(options => {
    options.ServerInfo = new Implementation
    {
        Name = "RegCheck",
        Version = "1.0.0",
        Title = "RegCheck Vehicle Lookup",
        Description = "Vehicle registration lookup across 50+ countries",
        WebsiteUrl = "https://www.regcheck.org.uk",
        Icons =
        [
            new Icon
            {
                Source   = "https://regcheck.org.uk/assets/ico/favicon.png",
                MimeType = "image/png",
                Sizes    = ["97x97"],
                Theme    = "light"
            }
        ]
    };
})
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<VehicleLookupTools>();

var app = builder.Build();



app.MapGet("/authorize", (HttpContext ctx) => AuthHandlers.Authorize(ctx));
app.MapGet("/login", (HttpContext ctx) => AuthHandlers.LoginPage(ctx));
app.MapPost("/login", async (HttpContext ctx) => await AuthHandlers.LoginPost(ctx));
app.MapPost("/token", async (HttpContext ctx) => await AuthHandlers.Token(ctx));
app.MapGet("/.well-known/oauth-authorization-server", (HttpContext ctx) =>
{
    var baseUrl = "https://regcheckmcp-526628810409.europe-west2.run.app";
    ctx.Response.ContentType = "application/json";
    return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        issuer = baseUrl,
        authorization_endpoint = $"{baseUrl}/authorize",
        token_endpoint = $"{baseUrl}/token",
        response_types_supported = new[] { "code" },
        code_challenge_methods_supported = new[] { "S256" }
    }));
});
app.MapMcp("/mcp");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");