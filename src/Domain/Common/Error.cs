namespace KartCategoryService.Domain.Common;

/// <summary>
/// Domain/business error, returned via Result rather than thrown (api-standards.md, "Domain/business
/// errors use a Result/Either pattern - not exceptions"). Code values match api-contract.yaml's
/// Problem.code (e.g. "max_depth_exceeded").
/// </summary>
public sealed class Error
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public string Code { get; }
    public string Message { get; }

    private Error(string code, string message)
    {
        Code = code;
        Message = message;
    }

    public static Error Validation(string message) => new("validation_error", message);

    public static Error NotFound(string message) => new("not_found", message);

    public static Error MaxDepthExceeded(string message) => new("max_depth_exceeded", message);

    public static Error CircularReference(string message) => new("circular_reference", message);
}
