using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Domain.Common;
using MediatR;

namespace KartCategoryService.Application.Features.DeprecateAttribute;

public sealed class DeprecateAttributeCommandHandler : IRequestHandler<DeprecateAttributeCommand, Result>
{
    private readonly IAttributeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public DeprecateAttributeCommandHandler(
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

    public async Task<Result> Handle(DeprecateAttributeCommand request, CancellationToken cancellationToken)
    {
        var attribute = await _repository.GetActiveByIdAsync(request.AttributeId, cancellationToken);
        if (attribute is null)
        {
            return Result.Failure(Error.NotFound($"Attribute '{request.AttributeId}' is not found or already deprecated."));
        }

        var deprecateResult = attribute.Deprecate(_currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        if (deprecateResult.IsFailure)
        {
            return deprecateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
