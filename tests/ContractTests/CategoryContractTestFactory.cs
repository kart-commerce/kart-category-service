using Kart.Shared.Messaging;
using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Attributes;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace KartCategoryService.ContractTests;

/// <summary>
/// Boots the real Api + Application pipeline but swaps PostgreSQL/Redis for in-memory fakes and
/// real Identity-issued JWT validation for a header-driven test scheme - these tests check the
/// HTTP wire contract (status codes, JSON field names, RBAC gating) against api-contract.yaml,
/// not persistence/caching/token-signing mechanics (already covered elsewhere).
/// </summary>
public sealed class CategoryContractTestFactory : WebApplicationFactory<Program>
{
    public InMemoryCategoryRepository Repository { get; } = new();

    public InMemoryAttributeRepository AttributeRepository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tells StartupConnectivityChecks to skip itself — this factory swaps the real
        // repository/cache/messaging registrations below for in-memory fakes, so there's
        // nothing for it to connect to.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICategoryRepository>();
            services.AddSingleton<ICategoryRepository>(Repository);

            services.RemoveAll<ICategoryCache>();
            services.AddSingleton<ICategoryCache, NullCategoryCache>();

            services.RemoveAll<IAttributeRepository>();
            services.AddSingleton<IAttributeRepository>(AttributeRepository);

            services.RemoveAll<IUnitOfWork>();
            services.AddSingleton<IUnitOfWork, NoOpUnitOfWork>();

            // No real RabbitMQ in the contract-test environment - these tests assert HTTP shape,
            // not event publication (already covered separately for CAT-2's outbox behavior).
            RemoveHostedService<RabbitMqTopologyStartupHostedService>(services);
            RemoveHostedService<OutboxRelayHostedService>(services);

            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
            });
        });
    }

    private static void RemoveHostedService<T>(IServiceCollection services)
        where T : class, IHostedService
    {
        var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(T));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
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

    public Task<Category?> GetActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var match = Categories.FirstOrDefault(c => c.Id == categoryId && c.Status == CategoryStatus.Active);
        return Task.FromResult(match);
    }

    public Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        Categories.Add(category);
        return Task.CompletedTask;
    }

    public Task<Category?> GetForUpdateAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var match = Categories.FirstOrDefault(c => c.Id == categoryId);
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<Category>> GetDescendantsForUpdateAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var descendants = Categories
            .Where(c => c.Status == CategoryStatus.Active && c.AncestorPath.Contains(categoryId))
            .ToList();
        return Task.FromResult<IReadOnlyList<Category>>(descendants);
    }
}

public sealed class InMemoryAttributeRepository : IAttributeRepository
{
    public List<ProductAttribute> Attributes { get; } = new();

    public Task<IReadOnlyList<ProductAttribute>> ListAsync(Guid? categoryId, bool includeDeprecated, CancellationToken cancellationToken)
    {
        IEnumerable<ProductAttribute> query = categoryId is { } id
            ? Attributes.Where(a => a.CategoryId == id || a.CategoryId == null)
            : Attributes;

        if (!includeDeprecated)
        {
            query = query.Where(a => a.Status == AttributeStatus.Active);
        }

        return Task.FromResult<IReadOnlyList<ProductAttribute>>(query.OrderBy(a => a.Name).ToList());
    }

    public Task<ProductAttribute?> GetActiveByIdAsync(Guid attributeId, CancellationToken cancellationToken)
    {
        var match = Attributes.FirstOrDefault(a => a.Id == attributeId && a.Status == AttributeStatus.Active);
        return Task.FromResult(match);
    }

    public Task AddAsync(ProductAttribute attribute, CancellationToken cancellationToken)
    {
        Attributes.Add(attribute);
        return Task.CompletedTask;
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

/// <summary>InMemoryCategoryRepository.AddAsync already applies the change synchronously - no real transaction to commit.</summary>
public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
