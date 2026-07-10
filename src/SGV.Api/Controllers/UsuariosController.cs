using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Api.Infrastructure.Results;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Api.Controllers;

[ApiController]
[Route("api/v1/usuarios")]
[Produces("application/json")]
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class UsuariosController(
    IUsuarioServicioConsulta consulta,
    IUsuarioServicioComandos comandos,
    IRolServicioConsulta roles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await consulta.ListAsync(cancellationToken));
    }

    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRoles(CancellationToken cancellationToken)
    {
        return Ok(await roles.ListAsync(cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Create(CrearUsuarioRequest request, CancellationToken cancellationToken)
    {
        var result = await comandos.CrearAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAll), new { id = result.Value!.Id }, result.Value)
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    [HttpPut("{userId}/roles")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> AssignRoles(
        string userId,
        AsignarRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await comandos.AsignarRolesAsync(userId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }
}
