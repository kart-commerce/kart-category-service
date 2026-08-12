using System.Diagnostics;
using Bogus;
using KartCategoryService.Domain.Categories;
using KartCategoryService.Infrastructure.Persistence;

namespace KartCategoryService.CategorySeeder;

/// <summary>
/// Builds a valid category tree in memory - via Category.CreateRoot/CreateChild, so every node still
/// goes through the domain's own max-depth/cycle/name-validation invariants - and flushes it to
/// Postgres in batches. Runs entirely outside the Api/Infrastructure DI graph: no RabbitMQ, no Redis,
/// just CategoryDbContext talking to Postgres directly, the same way CategoryDbContextFactory does
/// for `dotnet ef`.
/// </summary>
public sealed class CategoryTreeSeeder(CategoryDbContext db, SeedOptions options)
{
    public async Task<SeedResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var faker = options.RandomSeed is { } seed ? new Faker { Random = new Randomizer(seed) } : new Faker();
        var rng = options.RandomSeed is { } rngSeed ? new Random(rngSeed) : new Random();
        var nameGenerator = new CategoryNameGenerator(faker);

        // Only categories below the configured max depth are eligible parents for new children -
        // this is how --max-depth caps the tree shallower than the domain's hard limit of 4 without
        // needing its own check (CreateChild already rejects anything that would exceed Category.MaxDepth).
        var eligibleParents = new List<Category>();
        var batch = new List<Category>(options.BatchSize);

        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var stopwatch = Stopwatch.StartNew();
        var created = 0;
        var outboxRowsWritten = 0;

        for (var i = 0; i < options.Count; i++)
        {
            var isRoot = eligibleParents.Count == 0 || rng.NextDouble() < options.RootRatio;

            Category category;
            if (isRoot)
            {
                var name = nameGenerator.NextName(depth: 1);
                category = Category.CreateRoot(name, options.ActingPrincipal, DateTimeOffset.UtcNow).Value;
            }
            else
            {
                var parent = eligibleParents[rng.Next(eligibleParents.Count)];
                var name = nameGenerator.NextName(parent.Depth + 1);
                category = Category.CreateChild(name, parent, options.ActingPrincipal, DateTimeOffset.UtcNow).Value;
            }

            if (options.EmitEvents)
            {
                outboxRowsWritten += category.DomainEvents.Count;
            }
            else
            {
                category.ClearDomainEvents();
            }

            batch.Add(category);
            if (category.Depth < options.MaxDepth)
            {
                eligibleParents.Add(category);
            }

            if (batch.Count >= options.BatchSize)
            {
                await FlushAsync(batch, cancellationToken);
                created += batch.Count;
                batch.Clear();
                ReportProgress(created, stopwatch);
            }
        }

        if (batch.Count > 0)
        {
            await FlushAsync(batch, cancellationToken);
            created += batch.Count;
            ReportProgress(created, stopwatch);
        }

        stopwatch.Stop();
        return new SeedResult(created, outboxRowsWritten, stopwatch.Elapsed);
    }

    private async Task FlushAsync(List<Category> batch, CancellationToken cancellationToken)
    {
        db.Categories.AddRange(batch);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static void ReportProgress(int created, Stopwatch stopwatch)
    {
        var rate = created / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
        Console.WriteLine($"  {created:N0} categories inserted ({rate:N0} rows/sec)");
    }
}

public readonly record struct SeedResult(int CategoriesCreated, int OutboxRowsWritten, TimeSpan Elapsed);
