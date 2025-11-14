using EseeBridge.Auth;
using EseeBridge.Services;

using Microsoft.AspNetCore.Authentication;

using Scalar.AspNetCore;

using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IEseeBluetoothBridgeService, EseeBluetoothBridgeService>();

builder.Logging.ClearProviders();
// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()); // Or other sinks like File, Seq, etc.

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseDefaultFiles(); // Enable default file mapping (e.g., index.html)
app.UseStaticFiles(); // Enable serving static files
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync("http://0.0.0.0:5200");