using System.IdentityModel.Tokens.Jwt;
using KartCategoryService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace KartCategoryService.Infrastructure.Security;

/// <summary>
/// Resolves BRD S24.3's acting principal from the caller's Identity-issued access token `sub`
/// claim (kart-identity-service's JwtAccessTokenGenerator: `new(JwtRegisteredClaimNames.Sub,
/// subject)` - the client-credentials service-principal id for every Category write, per
/// ADR-0010). Falls back to a well-known system id outside an HTTP request (there is none for
/// this service today - every write is API-triggered - but this keeps the contract total).
/// </summary>
public sealed class HttpCurrentPrincipal : ICurrentPrincipal
{
    private const string UnknownPrincipal = "system:unknown";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentPrincipal(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string ActingPrincipal =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? UnknownPrincipal;
}
