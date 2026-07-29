using KartCategoryService.CategorySeeder;
using KartCategoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

SeedOptions options;
try
{
    options = SeedOptions.Parse(args);
}
catch (ArgUsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var connectionString =
    options.ConnectionString
    ?? Environment.GetEnvironmentVariable("CATEGORY_DB_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=kart_category;Username=postgres;Password=postgres";

Console.WriteLine($"Seeding {options.Count:N0} categories (batch size {options.BatchSize:N0}, max depth {options.MaxDepth}, " +
                   $"{(options.EmitEvents ? "emitting" : "not emitting")} outbox events)...");

var optionsBuilder = new DbContextOptionsBuilder<CategoryDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var db = new CategoryDbContext(optionsBuilder.Options);

try
{
    await db.Database.CanConnectAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not connect to the database: {ex.Message}");
    Console.Error.WriteLine("Is CATEGORY_DB_CONNECTION_STRING correct, and have migrations been applied (scripts/migrate.sh)?");
    return 1;
}

var seeder = new CategoryTreeSeeder(db, options);
var result = await seeder.RunAsync();

Console.WriteLine();
Console.WriteLine($"Done: {result.CategoriesCreated:N0} categories" +
                   (options.EmitEvents ? $", {result.OutboxRowsWritten:N0} outbox events" : string.Empty) +
                   $" in {result.Elapsed.TotalSeconds:N1}s.");

return 0;
