using KartCategoryService.Domain.Categories;

namespace KartCategoryService.CategorySeeder;

/// <summary>Parsed CLI arguments for a single seeding run.</summary>
public sealed class SeedOptions
{
    public required int Count { get; init; }
    public int BatchSize { get; init; } = 2000;
    public int MaxDepth { get; init; } = Category.MaxDepth;
    public double RootRatio { get; init; } = 0.05;
    public bool EmitEvents { get; init; }
    public int? RandomSeed { get; init; }
    public string? ConnectionString { get; init; }
    public string ActingPrincipal { get; init; } = "system:category-seeder";

    public static SeedOptions Parse(string[] args)
    {
        if (args.Length == 0 || args is ["-h" or "--help"])
        {
            throw new ArgUsageException(Usage);
        }

        if (!int.TryParse(args[0], out var count) || count <= 0)
        {
            throw new ArgUsageException($"<count> must be a positive integer, got '{args[0]}'.\n\n{Usage}");
        }

        int batchSize = 2000;
        int maxDepth = Category.MaxDepth;
        double rootRatio = 0.05;
        bool emitEvents = false;
        int? seed = null;
        string? connectionString = null;
        string principal = "system:category-seeder";

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--batch-size":
                    batchSize = int.Parse(RequireValue(args, ref i, "--batch-size"));
                    break;
                case "--max-depth":
                    maxDepth = int.Parse(RequireValue(args, ref i, "--max-depth"));
                    break;
                case "--root-ratio":
                    rootRatio = double.Parse(RequireValue(args, ref i, "--root-ratio"));
                    break;
                case "--emit-events":
                    emitEvents = true;
                    break;
                case "--seed":
                    seed = int.Parse(RequireValue(args, ref i, "--seed"));
                    break;
                case "--connection":
                    connectionString = RequireValue(args, ref i, "--connection");
                    break;
                case "--principal":
                    principal = RequireValue(args, ref i, "--principal");
                    break;
                default:
                    throw new ArgUsageException($"Unknown option '{args[i]}'.\n\n{Usage}");
            }
        }

        if (batchSize <= 0)
        {
            throw new ArgUsageException("--batch-size must be a positive integer.");
        }

        if (maxDepth is < 1 or > Category.MaxDepth)
        {
            throw new ArgUsageException($"--max-depth must be between 1 and {Category.MaxDepth} (the domain's hard cap).");
        }

        if (rootRatio is < 0 or > 1)
        {
            throw new ArgUsageException("--root-ratio must be between 0 and 1.");
        }

        return new SeedOptions
        {
            Count = count,
            BatchSize = batchSize,
            MaxDepth = maxDepth,
            RootRatio = rootRatio,
            EmitEvents = emitEvents,
            RandomSeed = seed,
            ConnectionString = connectionString,
            ActingPrincipal = principal,
        };
    }

    private static string RequireValue(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgUsageException($"'{option}' requires a value.");
        }

        return args[++i];
    }

    private const string Usage = """
        Usage: category-seeder <count> [options]

          <count>              How many categories to create in total.

        Options:
          --batch-size <n>     Rows per DB round-trip (default: 2000).
          --max-depth <n>      Cap tree depth, 1-4 (default: 4, the domain's own hard cap).
          --root-ratio <0-1>   Fraction of nodes created as new depth-1 roots rather than
                               attached under an existing category (default: 0.05).
          --emit-events        Also write category_outbox_events rows, so the outbox relay
                               publishes CategoryUpdated for seeded data (default: off - seed
                               runs don't spam RabbitMQ unless you ask for it).
          --seed <n>           Deterministic RNG seed, for reproducible fake data.
          --connection <str>   Postgres connection string (default: $CATEGORY_DB_CONNECTION_STRING,
                               falling back to the same localhost default CategoryDbContextFactory uses).
          --principal <str>    created_by/updated_by value stamped on seeded rows
                               (default: system:category-seeder).

        Examples:
          category-seeder 500
          category-seeder 100000 --batch-size 5000 --seed 42
        """;
}

public sealed class ArgUsageException(string message) : Exception(message);
