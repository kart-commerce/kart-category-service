using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Infrastructure.Caching;
using KartCategoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KartCategoryService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CategoryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CategoryDatabase")));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") ?? "localhost:6379"));

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryCache, RedisCategoryCache>();

        return services;
    }
}
