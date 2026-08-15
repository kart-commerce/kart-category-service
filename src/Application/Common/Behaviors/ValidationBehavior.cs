using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Common.Behaviors;

/// <summary>
/// Runs every registered FluentValidation validator for a request before its Handler executes
/// (api-standards.md: "Input validated at the API boundary"). A request type with no registered
/// validator (e.g. a query with nothing to validate) passes through untouched - no empty
/// ceremonial Validator.cs is required per slice.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .ToList();

            if (failures.Count > 0)
            {
                var requestName = typeof(TRequest).Name;

                // checkpoint-logging-standard.md taxonomy stage 4 ("<Rule>ValidationFailed",
                // logged at Warning with the reason before throwing) generalized here for every
                // FluentValidation validator, rather than duplicated per handler - the
                // ValidationException itself is still logged once more, generically, at the API
                // boundary by GlobalExceptionHandler; this line is the one that's greppable by
                // Stage and carries the actual field-level reasons.
                _logger.LogWarning(
                    "Stage {Stage}: {RequestName} rejected - {Errors}",
                    $"{requestName}ValidationFailed",
                    requestName,
                    string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
