using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Features.UpdateAttribute;

public sealed class UpdateAttributeCommandHandler : IRequestHandler<UpdateAttributeCommand, Result<AttributeDto>>
{
    private readonly IAttributeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateAttributeCommandHandler> _logger;

    public UpdateAttributeCommandHandler(
        IAttributeRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<UpdateAttributeCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<AttributeDto>> Handle(UpdateAttributeCommand request, CancellationToken cancellationToken)
    {
        var attribute = await _repository.GetActiveByIdAsync(request.AttributeId, cancellationToken);
        if (attribute is null)
        {
            _logger.LogWarning(
                "Stage {Stage}: update-attribute rejected, attribute {AttributeId} not found or already deprecated",
                "AttributeUpdateRejected",
                request.AttributeId);
            return Result.Failure<AttributeDto>(Error.NotFound($"Attribute '{request.AttributeId}' is not found or already deprecated."));
        }

        var values = request.Values.Select(v => (v.Value, v.DisplayOrder)).ToList();
        var updateResult = attribute.Update(request.Name, values, _currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (updateResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: update-attribute rejected for attribute {AttributeId} ({ErrorCode}): {Reason}",
                "AttributeUpdateRejected",
                request.AttributeId,
                updateResult.Error.Code,
                updateResult.Error.Message);
            return Result.Failure<AttributeDto>(updateResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: attribute {AttributeId} updated",
            "AttributeUpdateProcessCompleted",
            attribute.Id);

        return Result.Success(AttributeDto.FromDomain(attribute));
    }
}
