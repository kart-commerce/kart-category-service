namespace KartCategoryService.Domain.Attributes;

/// <summary>
/// Which write triggered an AttributeUpdated event - mirrors CategoryOperation's rationale
/// (event-contract.md: "operation is added so a consumer does not have to infer which write
/// triggered the event from field diffs").
/// </summary>
public enum AttributeOperation
{
    Created,
    Updated,
    Deprecated
}
