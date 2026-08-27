using System.Text.Json;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using StackExchange.Redis;

namespace KartCategoryService.Infrastructure.Caching;

/// <summary>
/// database-design.md's `category:children:{parentId}` write-through cache (sentinel "root" for
/// depth-1 nodes). A miss returns null so the caller falls back to ICategoryRepository - Redis
/// availability is a latency, not a correctness, dependency (ADR-0011).
/// </summary>
public sealed class RedisCategoryCache : ICategoryCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisCategoryCache(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<IReadOnlyList<CategoryDto>?> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(ChildrenKey(parentId));
        return value.HasValue
            ? JsonSerializer.Deserialize<List<CategoryDto>>((string)value!, SerializerOptions)
            : null;
    }

    public async Task SetChildrenAsync(Guid? parentId, IReadOnlyList<CategoryDto> children, CancellationToken cancellationToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var json = JsonSerializer.Serialize(children, SerializerOptions);
        await database.StringSetAsync(ChildrenKey(parentId), json);
    }

    private static string ChildrenKey(Guid? parentId) => $"category:children:{parentId?.ToString() ?? "root"}";
}
