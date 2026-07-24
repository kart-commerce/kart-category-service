using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Categories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KartCategoryService.ContractTests;

/// <summary>
/// Boots the real Api + Application pipeline but swaps PostgreSQL/Redis for in-memory fakes -
/// these tests check the HTTP wire contract (status codes, JSON field names) against
/// api-contract.yaml, not persistence/caching behavior (already covered by UnitTests/IntegrationTests).
/// </summary>
public sealed class CategoryContractTestFactory : WebApplicationFactory<Program>
{
    public InMemoryCategoryRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICategoryRepository>();
            services.AddSingleton<ICategoryRepository>(Repository);

            services.RemoveAll<ICategoryCache>();
            services.AddSingleton<ICategoryCache, NullCategoryCache>();
        });
    }
}

public sealed class InMemoryCategoryRepository : ICategoryRepository
{
    public List<Category> Categories { get; } = new();

    public Task<IReadOnlyList<Category>> GetChildrenAsync(Guid? parentId, bool includeDeprecated, CancellationToken cancellationToken)
    {
        IEnumerable<Category> query = Categories.Where(c => c.ParentId == parentId);
        if (!includeDeprecated)
        {
            query = query.Where(c => c.Status == CategoryStatus.Active);
        }

        return Task.FromResult<IReadOnlyList<Category>>(query.OrderBy(c => c.Name).ToList());
    }
}

/// <summary>Always a miss - contract tests exercise the repository fallback path deterministically.</summary>
public sealed class NullCategoryCache : ICategoryCache
{
    public Task<IReadOnlyList<CategoryDto>?> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoryDto>?>(null);

    public Task SetChildrenAsync(Guid? parentId, IReadOnlyList<CategoryDto> children, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
