using AutoReparos.API;
using AutoReparos.API.Endpoints;
using AutoReparos.Application;
using AutoReparos.Infra.Data;
using AutoReparos.Infra.IoC;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Logs estruturados em JSON com correlacao de TraceId/SpanId.
// Precisa vir ANTES de AddOpenTelemetryObservability, que registra o provider OTel
// em builder.Logging - um ClearProviders() posterior descartaria o exportador OTLP.
builder.Logging.ClearProviders();
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId
        | ActivityTrackingOptions.SpanId
        | ActivityTrackingOptions.ParentId;
});
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    options.UseUtcTimestamp = true;
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

builder.Services.AddAPI(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInfraestructureSwagger();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks();
builder.Services.AddOpenTelemetryObservability(builder.Configuration, builder.Logging);

var app = builder.Build();

await DbInitializer.SeedDataAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI(options =>
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AutoReparos API v1"));

app.UseExceptionHandler();

app.UseCors("AllowFrontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapUsuariosEndpoints();
app.MapClientesEndpoints();
app.MapVeiculosEndpoints();
app.MapServicosEndpoints();
app.MapInsumosEndpoints();
app.MapOrdemServicoEndpoints();
app.MapDashboardEndpoints();

app.MapHealthChecks("/health");

app.Run();
