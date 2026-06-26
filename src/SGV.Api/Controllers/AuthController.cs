using Microsoft.AspNetCore.Mvc;
using SGV.Api.Contracts;
using SGV.Aplicacion.Seguridad.Usuarios;

namespace SGV.Api.Controllers;

[ApiController]
[Route(AuthApiRoutes.Base)]
[Produces("application/json")]
public sealed class AuthController(IAuthServicio authServicio) : ControllerBase
{
    [HttpPost(AuthApiRoutes.LoginRelative)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authServicio.LoginAsync(request, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }
}
