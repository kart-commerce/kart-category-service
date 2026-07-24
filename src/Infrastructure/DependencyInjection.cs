using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Infrastructure.Caching;
using KartCategoryService.Infrastructure.Messaging;
using KartCategoryService.Infrastructure.Persistence;
using KartCategoryService.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
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
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentPrincipal, HttpCurrentPrincipal>();

        // IConnectionFactory only builds config - it does not connect eagerly, so registering it
        // here is safe even if RabbitMQ is unreachable at startup (OutboxRelayHostedService owns
        // the actual, retrying connection attempt).
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            HostName = configuration["RabbitMq:HostName"] ?? "localhost",
            DispatchConsumersAsync = true,
        });
        services.AddHostedService<OutboxRelayHostedService>();

        return services;
    }
}
