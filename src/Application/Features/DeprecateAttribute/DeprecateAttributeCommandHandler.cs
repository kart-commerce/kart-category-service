using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Features.DeprecateAttribute;

public sealed class DeprecateAttributeCommandHandler : IRequestHandler<DeprecateAttributeCommand, Result>
{
    private readonly IAttributeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeprecateAttributeCommandHandler> _logger;

    public DeprecateAttributeCommandHandler(
        IAttributeRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<DeprecateAttributeCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(DeprecateAttributeCommand request, CancellationToken cancellationToken)
    {
        var attribute = await _repository.GetActiveByIdAsync(request.AttributeId, cancellationToken);
        if (attribute is null)
        {
            _logger.LogWarning(
                "Stage {Stage}: deprecate-attribute rejected, attribute {AttributeId} not found or already deprecated",
                "AttributeDeprecateRejected",
                request.AttributeId);
            return Result.Failure(Error.NotFound($"Attribute '{request.AttributeId}' is not found or already deprecated."));
        }

        var deprecateResult = attribute.Deprecate(_currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (deprecateResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: deprecate-attribute rejected for attribute {AttributeId} ({ErrorCode}): {Reason}",
                "AttributeDeprecateRejected",
                request.AttributeId,
                deprecateResult.Error.Code,
                deprecateResult.Error.Message);
            return deprecateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: attribute {AttributeId} deprecated",
            "AttributeDeprecateProcessCompleted",
            attribute.Id);

        return Result.Success();
    }
}
