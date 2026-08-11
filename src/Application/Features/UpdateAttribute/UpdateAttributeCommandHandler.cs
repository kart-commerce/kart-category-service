using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.UpdateAttribute;

public sealed class UpdateAttributeCommandHandler : IRequestHandler<UpdateAttributeCommand, Result<AttributeDto>>
{
    private readonly IAttributeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public UpdateAttributeCommandHandler(
        IAttributeRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AttributeDto>> Handle(UpdateAttributeCommand request, CancellationToken cancellationToken)
    {
        var attribute = await _repository.GetActiveByIdAsync(request.AttributeId, cancellationToken);
        if (attribute is null)
        {
            return Result.Failure<AttributeDto>(Error.NotFound($"Attribute '{request.AttributeId}' is not found or already deprecated."));
        }

        var values = request.Values.Select(v => (v.Value, v.DisplayOrder)).ToList();
        var updateResult = attribute.Update(request.Name, values, _currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (updateResult.IsFailure)
        {
            return Result.Failure<AttributeDto>(updateResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(AttributeDto.FromDomain(attribute));
    }
}
