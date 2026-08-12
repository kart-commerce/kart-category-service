using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KartCategoryService.Api.Security;

/// <summary>
/// api-contract.yaml's `clientCredentials` security scheme: an Identity-issued RS256 JWT, checked
/// structurally here (signature + expiry), never re-deriving role grants locally (BRD S24.1 -
/// Identity is the sole issuer of platform role claims). "AdminOnly" gates every RBAC-gated
/// write endpoint (create/rename/move/deprecate) on kart-identity-service's actual claim shape:
/// `new Claim("roles", role)` per its JwtAccessTokenGenerator, value "admin" for the
/// Admin-scoped service principal Admin Service authenticates as (ADR-0010).
/// </summary>
public static class AuthenticationExtensions
{
    public const string AdminPolicy = "AdminOnly";
    private const string RolesClaimType = "roles";
    private const string AdminRoleValue = "admin";

    public static IServiceCollection AddCategoryAuthentication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient<JwksSigningKeyResolver>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksSigningKeyResolver>((options, resolver) =>
            {
                // Real bug found live-verifying the Category & Attribute Management (Admin)
                // flow: every write endpoint (create/rename/move/deprecate/reorder) 403'd for
                // every caller, always - never previously exercised end-to-end. The JWT bearer
                // handler's default inbound-claim mapping silently renames the token's literal
                // "roles" claim to the long ClaimTypes.Role URI
                // (http://schemas.microsoft.com/ws/2008/06/identity/claims/role) before this
                // policy's RequireClaim("roles", "admin") ever runs against it - so the claim
                // this policy looks for was never actually present under that name. MapInboundClaims
                // = false keeps the token's own claim types (matching this class's own doc comment,
                // which already documented the *intended* shape - `new Claim("roles", role)` - just
                // not that the default handler mutates it before Authorization sees it).
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Identity's JwtAccessTokenGenerator sets neither `iss` nor `aud` on the tokens
                    // it mints - validating either here would reject every real token.
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = resolver.ResolveSigningKeys,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireClaim(RolesClaimType, AdminRoleValue));

        return services;
    }
}
