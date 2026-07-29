using KartCategoryService.Api;
using KartCategoryService.Api.HealthChecks;
using KartCategoryService.Api.Middleware;
using KartCategoryService.Api.Security;
using KartCategoryService.Application;
using KartCategoryService.Infrastructure;
using Kart.Shared.Configuration;
using Kart.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service. See appsettings.Local.json.example.
builder.AddKartGlobalConfig();

// kart-conventions.md Observability section: Serilog + OpenTelemetry SDK behind one DI call.
builder.AddKartObservability("kart-category-service");

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// /health/live: process is up, no dependency check. /health/ready: this service's job depends
// on Postgres being reachable AND migrated (a connectable-but-unmigrated database, e.g. a
// missing category_outbox_events table, is not "ready") - matching kart-infra's service-chart
// probe convention.
builder.Services.AddHealthChecks()
    .AddCheck<CategoryDbHealthCheck>("category-db", tags: ["ready"]);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCategoryAuthentication();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

await StartupConnectivityChecks.RunAsync(app);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Per-HTTP-request Information log (method/path/status/elapsed) - the RED-style access log
// observability-standards.md expects on every endpoint, for free. Registered outermost, wrapping
// UseExceptionHandler below, so this always logs the *final* status code a client actually
// received (e.g. 400 for a handled ValidationException) - nested the other way round, Serilog's
// request logger observes the exception before the handler translates it and always misreports
// every handled exception as a 500.
app.UseSerilogRequestLogging();

// The single global error handler - every unhandled exception (including FluentValidation's
// ValidationException, thrown by ValidationBehavior and never caught in the MediatR pipeline)
// is translated to api-contract.yaml's Problem shape and logged here, so no controller/handler
// needs its own try/catch.
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<CategoryContextEnrichmentMiddleware>();
app.UseAuthorization();

// Prometheus scrape target (observability-standards.md's mandatory `/metrics`).
app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program
{
}
