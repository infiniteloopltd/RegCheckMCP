

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("regcheck");

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<VehicleLookupTools>();

var app = builder.Build();

// Read the API key from the header at connection time and stash it in the scoped service


app.MapMcp("/mcp");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");