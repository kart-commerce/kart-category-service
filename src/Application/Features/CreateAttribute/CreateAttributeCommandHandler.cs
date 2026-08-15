using KartCategoryService.Application.Common.Interfaces;
using KartCategoryService.Application.Common.Models;
using KartCategoryService.Domain.Attributes;
using KartCategoryService.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartCategoryService.Application.Features.CreateAttribute;

public sealed class CreateAttributeCommandHandler : IRequestHandler<CreateAttributeCommand, Result<AttributeDto>>
{
    private readonly IAttributeRepository _attributeRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateAttributeCommandHandler> _logger;

    public CreateAttributeCommandHandler(
        IAttributeRepository attributeRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<CreateAttributeCommandHandler> logger)
    {
        _attributeRepository = attributeRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<AttributeDto>> Handle(CreateAttributeCommand request, CancellationToken cancellationToken)
    {
        if (request.CategoryId is { } categoryId)
        {
            var category = await _categoryRepository.GetActiveByIdAsync(categoryId, cancellationToken);
            if (category is null)
            {
                _logger.LogWarning(
                    "Stage {Stage}: create-attribute rejected, categoryId {CategoryId} not found or not active",
                    "AttributeCreateRejected",
                    categoryId);
                return Result.Failure<AttributeDto>(Error.NotFound($"categoryId '{categoryId}' not found or not active."));
            }
        }

        var dataType = Enum.Parse<AttributeDataType>(request.DataType, ignoreCase: true);
        var values = request.Values.Select(v => (v.Value, v.DisplayOrder)).ToList();
        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        var creationResult = ProductAttribute.Create(request.Name, request.CategoryId, dataType, values, actingPrincipal, now);
        if (creationResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: create-attribute rejected ({ErrorCode}): {Reason}",
                "AttributeCreateRejected",
                creationResult.Error.Code,
                creationResult.Error.Message);
            return Result.Failure<AttributeDto>(creationResult.Error);
        }

        var attribute = creationResult.Value;
        await _attributeRepository.AddAsync(attribute, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Stage {Stage}: attribute {AttributeId} ({Name}) created for category {CategoryId}",
            "AttributeCreateProcessCompleted",
            attribute.Id,
            attribute.Name,
            attribute.CategoryId);

        return Result.Success(AttributeDto.FromDomain(attribute));
    }
}
