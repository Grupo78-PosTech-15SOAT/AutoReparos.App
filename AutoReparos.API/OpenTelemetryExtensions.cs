using AutoReparos.Application.Shared.Metrics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AutoReparos.API
{
    public static class OpenTelemetryExtensions
    {
        public static IServiceCollection AddOpenTelemetryObservability(
            this IServiceCollection services,
            IConfiguration configuration,
            ILoggingBuilder loggingBuilder)
        {
            var otelEndpoint = configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
            var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "autoreparos-api";

            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName: serviceName, serviceVersion: "1.0.0");

            // Quando uma license key é fornecida, os sinais podem ser enviados direto ao backend
            // do vendor (ex: https://otlp.nr-data.net:4317) sem passar pelo OTel Collector local.
            var vendorLicenseKey = configuration["NEW_RELIC_LICENSE_KEY"]
                ?? configuration["OpenTelemetry:NewRelicLicenseKey"];

            Action<OtlpExporterOptions> configureOtlp = options =>
            {
                options.Endpoint = new Uri(otelEndpoint);
                if (!string.IsNullOrWhiteSpace(vendorLicenseKey))
                {
                    options.Headers = $"api-key={vendorLicenseKey}";
                }
            };

            services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing
                        .SetResourceBuilder(resourceBuilder)
                        .AddSource(serviceName)
                        .AddSource("Npgsql")
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;
                        })
                        .AddHttpClientInstrumentation()
                        .AddEntityFrameworkCoreInstrumentation(options =>
                        {
                            options.SetDbStatementForText = true;
                        })
                        .AddOtlpExporter(configureOtlp);
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .SetResourceBuilder(resourceBuilder)
                        .AddMeter(AutoReparosMetrics.MeterName)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation()
                        .AddOtlpExporter(configureOtlp);
                })
                .WithLogging(logs =>
                {
                    logs.SetResourceBuilder(resourceBuilder);
                    logs.AddOtlpExporter(configureOtlp);
                }, options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                    options.ParseStateValues = true;
                });

            loggingBuilder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                options.AddOtlpExporter(configureOtlp);
            });

            return services;
        }
    }
}


