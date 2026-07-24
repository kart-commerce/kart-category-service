namespace KartCategoryService.Api.Common;

/// <summary>RFC 7807-style problem details - api-contract.yaml's `Problem` schema exactly.</summary>
public sealed record ProblemDto(string Code, string Message, object? Details = null);
