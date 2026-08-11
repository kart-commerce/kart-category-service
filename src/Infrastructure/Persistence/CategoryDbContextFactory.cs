using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging.Abstractions;

namespace KartCategoryService.Infrastructure.Persistence;

/// <summary>
/// Design-time-only factory `dotnet ef migrations add`/`database update` use to
/// build <see cref="CategoryDbContext"/> without spinning up the full Api host (and
/// its own required configuration, e.g. GlobalConfig/JWT). Never used at runtime —
/// the app's own DI registration (Infrastructure/DependencyInjection.cs) takes over
/// there.
/// </summary>
public sealed class CategoryDbContextFactory : IDesignTimeDbContextFactory<CategoryDbContext>
{
    public CategoryDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CATEGORY_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kart_category;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<CategoryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CategoryDbContext(optionsBuilder.Options, NullLogger<CategoryDbContext>.Instance);
    }
}
