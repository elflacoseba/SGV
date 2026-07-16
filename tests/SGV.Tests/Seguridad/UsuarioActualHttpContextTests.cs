using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SGV.Api.Seguridad;
using SGV.Contracts.Seguridad;
using Xunit;

namespace SGV.Tests.Seguridad;

public sealed class UsuarioActualHttpContextTests
{
    [Fact]
    public void AuthenticatedPrincipal_ExposesIdentityPersonaRolesAndCorrelation()
    {
        var personaId = Guid.Parse("e2000000-0000-0000-0000-000000000001");
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-user-test",
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim("persona_id", personaId.ToString()),
                new Claim(ClaimTypes.Role, RolesSgv.Administrador),
                new Claim(ClaimTypes.Role, RolesSgv.Consultor)
            ], "Test"))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var current = new UsuarioActualHttpContext(accessor);

        Assert.Equal("user-123", current.UserId);
        Assert.Equal(personaId, current.PersonaId);
        Assert.Equal([RolesSgv.Administrador, RolesSgv.Consultor], current.Roles);
        Assert.NotNull(current.CorrelationId);
    }

    [Fact]
    public void MissingHttpContext_ExposesAnonymousValuesWithStableCorrelation()
    {
        var current = new UsuarioActualHttpContext(new HttpContextAccessor());

        var firstCorrelation = current.CorrelationId;

        Assert.Null(current.UserId);
        Assert.Null(current.PersonaId);
        Assert.Empty(current.Roles);
        Assert.NotNull(firstCorrelation);
        Assert.Equal(firstCorrelation, current.CorrelationId);
    }
}
