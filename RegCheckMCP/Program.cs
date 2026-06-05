

using RegCheckMcp.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("regcheck");

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<VehicleLookupTools>();

var app = builder.Build();



app.MapGet("/authorize", (HttpContext ctx) => AuthHandlers.Authorize(ctx));
app.MapGet("/login", (HttpContext ctx) => AuthHandlers.LoginPage(ctx));
app.MapPost("/login", async (HttpContext ctx) => await AuthHandlers.LoginPost(ctx));
app.MapPost("/token", async (HttpContext ctx) => await AuthHandlers.Token(ctx));

app.MapMcp("/mcp");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");