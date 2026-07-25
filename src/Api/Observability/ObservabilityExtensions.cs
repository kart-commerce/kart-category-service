using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;

namespace KartCategoryService.Api.Observability;

/// <summary>
/// observability-standards.md's mandated stack (Serilog -> Loki, OpenTelemetry -> Tempo/
/// Prometheus), wired locally in this composition root - kart-conventions.md's
/// `Kart.Shared.Observability` doesn't exist as a published package yet, so this is shaped to be
/// a drop-in replacement once it does (mirrors kart-identity-service's own interim wiring).
/// Category is not one of the four 100%-trace-coverage saga services (requirement-spec.md
/// Observability NFR row), but no custom sampler is configured here either - the OpenTelemetry
/// SDK's default (AlwaysOn) already covers "trace everything", and the reusable standard's
/// tiered sampling is the shared package's job once it exists, not something to hand-roll here.
/// </summary>
public static class ObservabilityExtensions
{
    public const string ServiceName = "kart-category-service";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = builder.Configuration["Observability:Otlp:Endpoint"];

        // Console sink emits structured JSON; shipping to Loki is the OTel Collector's
        // job (OTLP log exporter), never something this process does directly.
        builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.WithProperty("service", ServiceName)
            .WriteTo.Console(new CompactJsonFormatter()));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                // RED metrics (rate/errors/duration) on every HTTP endpoint, per
                // observability-standards.md - ASP.NET Core's own instrumentation
                // already emits http.server.request.duration; scraped at /metrics.
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return builder;
    }
}
