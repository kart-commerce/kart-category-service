using Bogus;

namespace KartCategoryService.CategorySeeder;

/// <summary>Produces plausible-looking category names - depth-1 nodes read as departments, deeper ones as narrower subcategories.</summary>
public sealed class CategoryNameGenerator(Faker faker)
{
    public string NextName(int depth) => depth == 1
        ? faker.Commerce.Department()
        : $"{faker.Commerce.ProductAdjective()} {faker.Commerce.Categories(1)[0]}";
}
