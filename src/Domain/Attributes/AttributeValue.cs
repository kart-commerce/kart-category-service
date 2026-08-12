namespace KartCategoryService.Domain.Attributes;

/// <summary>
/// One pick-list entry of a `Select`-typed ProductAttribute (e.g. "Red" under "Color"). Owned by
/// the ProductAttribute aggregate - never fetched, mutated, or referenced independently; the whole
/// Values collection is replaced as one unit by ProductAttribute.UpdateValues, matching Category's
/// own "one coarse event per logical operation" convention rather than emitting per-value events.
/// </summary>
public sealed class AttributeValue
{
    public Guid Id { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }

    // EF Core materialization only.
    private AttributeValue()
    {
    }

    public AttributeValue(Guid id, string value, int displayOrder)
    {
        Id = id;
        Value = value;
        DisplayOrder = displayOrder;
    }
}
