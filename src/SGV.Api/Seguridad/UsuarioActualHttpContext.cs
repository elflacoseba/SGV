using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SGV.Aplicacion.Seguridad;

namespace SGV.Api.Seguridad;

/// <summary>
/// Adapts the authenticated HTTP principal to the application current-user port.
/// </summary>
public sealed class UsuarioActualHttpContext : IUsuarioActual
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Guid _correlationId = Guid.NewGuid();

    public UsuarioActualHttpContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public Guid? PersonaId
        => Guid.TryParse(Principal?.FindFirstValue("persona_id"), out var personaId)
            ? personaId
            : null;

    public IReadOnlyCollection<string> Roles
        => Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    public Guid? CorrelationId => _correlationId;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;
}
