using KartCategoryService.Domain.Common;

namespace KartCategoryService.Domain.Attributes;

/// <summary>
/// This service's second aggregate root, added for the "Category &amp; Attribute Management
/// (Admin)" flow. A named, typed catalog field (e.g. "Color", "Size", "Warranty period"),
/// optionally scoped to one Category (CategoryId null = global, available to products in any
/// category). Named `ProductAttribute` rather than `Attribute` to avoid colliding with
/// System.Attribute throughout this codebase. Deliberately simpler than Category: no tree, no
/// move - only Create / Update (rename + replace the whole Values list as one operation, mirroring
/// Category's "one coarse event per logical write" convention) / Deprecate.
/// </summary>
public sealed class ProductAttribute : AggregateRoot
{
    private readonly List<AttributeValue> _values = new();

    public string Name { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public AttributeDataType DataType { get; private set; }
    public IReadOnlyList<AttributeValue> Values => _values.AsReadOnly();
    public AttributeStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    // EF Core materialization only - state is set via reflection over the fields above.
    private ProductAttribute()
    {
    }

    private ProductAttribute(
        Guid id,
        string name,
        Guid? categoryId,
        AttributeDataType dataType,
        IReadOnlyList<(string Value, int DisplayOrder)> values,
        string actingPrincipal,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        CategoryId = categoryId;
        DataType = dataType;
        _values = values.Select(v => new AttributeValue(Guid.NewGuid(), v.Value, v.DisplayOrder)).ToList();
        Status = AttributeStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = actingPrincipal;
        UpdatedBy = actingPrincipal;
    }

    /// <summary>api-contract.yaml createAttribute - POST /v1/attributes (RBAC-gated, Admin only).</summary>
    public static Result<ProductAttribute> Create(
        string name,
        Guid? categoryId,
        AttributeDataType dataType,
        IReadOnlyList<(string Value, int DisplayOrder)> values,
        string actingPrincipal,
        DateTimeOffset now)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<ProductAttribute>(Error.Validation("Attribute name is required."));
        }

        var valueError = ValidateValues(dataType, values);
        if (valueError is not null)
        {
            return Result.Failure<ProductAttribute>(valueError);
        }

        var attribute = new ProductAttribute(Guid.NewGuid(), trimmed, categoryId, dataType, values, actingPrincipal, now);
        attribute.Raise(attribute.ToDomainEvent(AttributeOperation.Created, now));
        return Result.Success(attribute);
    }

    /// <summary>
    /// api-contract.yaml updateAttribute - PATCH /v1/attributes/{attributeId}. Renames and replaces
    /// the entire Values list as one logical write (never a per-value add/remove endpoint) -
    /// CategoryId/DataType are immutable after creation to avoid the "changing DataType invalidates
    /// every product that already picked a value under the old type" edge case entirely, rather
    /// than handling it.
    /// </summary>
    public Result Update(string newName, IReadOnlyList<(string Value, int DisplayOrder)> newValues, string actingPrincipal, DateTimeOffset now)
    {
        if (Status != AttributeStatus.Active)
        {
            return Result.Failure(Error.NotFound($"Attribute '{Id}' is not found or already deprecated."));
        }

        var trimmed = newName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(Error.Validation("Attribute name is required."));
        }

        var valueError = ValidateValues(DataType, newValues);
        if (valueError is not null)
        {
            return Result.Failure(valueError);
        }

        Name = trimmed;
        _values.Clear();
        _values.AddRange(newValues.Select(v => new AttributeValue(Guid.NewGuid(), v.Value, v.DisplayOrder)));
        Touch(actingPrincipal, now);
        Raise(ToDomainEvent(AttributeOperation.Updated, now));
        return Result.Success();
    }

    /// <summary>
    /// Soft-deletes this attribute. The id is never physically removed/reused, matching Category's
    /// own convention - product-service's future consumer keys any denormalized copy by this id.
    /// </summary>
    public Result Deprecate(string actingPrincipal, DateTimeOffset now)
    {
        if (Status == AttributeStatus.Deprecated)
        {
            return Result.Failure(Error.NotFound($"Attribute '{Id}' is not found or already deprecated."));
        }

        Status = AttributeStatus.Deprecated;
        Touch(actingPrincipal, now);
        Raise(ToDomainEvent(AttributeOperation.Deprecated, now));
        return Result.Success();
    }

    private static Error? ValidateValues(AttributeDataType dataType, IReadOnlyList<(string Value, int DisplayOrder)> values)
    {
        if (dataType == AttributeDataType.Select)
        {
            if (values.Count == 0)
            {
                return Error.Validation("A Select attribute requires at least one value.");
            }

            if (values.Any(v => string.IsNullOrWhiteSpace(v.Value)))
            {
                return Error.Validation("Attribute values must not be blank.");
            }
        }
        else if (values.Count > 0)
        {
            return Error.Validation($"A {dataType} attribute does not accept a Values list.");
        }

        return null;
    }

    private AttributeUpdatedDomainEvent ToDomainEvent(AttributeOperation operation, DateTimeOffset now) => new(
        Id,
        Name,
        CategoryId,
        DataType,
        _values.Select(v => new AttributeValueSnapshot(v.Id, v.Value, v.DisplayOrder)).ToList(),
        operation,
        now);

    private void Touch(string actingPrincipal, DateTimeOffset now)
    {
        UpdatedBy = actingPrincipal;
        UpdatedAt = now;
    }
}
