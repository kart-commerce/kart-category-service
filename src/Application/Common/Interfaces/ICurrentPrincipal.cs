namespace KartCategoryService.Application.Common.Interfaces;

/// <summary>
/// Resolves the acting principal for BRD S24.3 audit stamping (created_by/updated_by). Every
/// taxonomy write is Admin's own client-credentials caller - Category has no self-service
/// end-user write path (requirement-spec.md S24.3).
/// </summary>
public interface ICurrentPrincipal
{
    string ActingPrincipal { get; }
}
