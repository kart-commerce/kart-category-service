using Serilog.Context;

namespace KartCategoryService.Api.Middleware;

/// <summary>
/// requirement-spec.md's Observability NFR row: structured logs carry `categoryId` alongside
/// the mandatory `traceId`/`service`/`level` fields. The route value is present on every write
/// endpoint (rename/move/deprecate all route on {categoryId:guid}) - a no-op for the two paths
/// that don't carry one (GET listCategories, POST createCategory, which has no id to attach
/// until the handler mints one).
/// </summary>
public sealed class CategoryContextEnrichmentMiddleware
{
    private const string RouteValueKey = "categoryId";

    private readonly RequestDelegate _next;

    public CategoryContextEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.RouteValues.TryGetValue(RouteValueKey, out var categoryId) || categoryId is null)
        {
            await _next(context);
            return;
        }

        using (LogContext.PushProperty("categoryId", categoryId))
        {
            await _next(context);
        }
    }
}
