using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Api.Infrastructure.Results;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Api.Controllers;

[ApiController]
[Route("api/v1/usuarios")]
[Produces("application/json")]
[Authorize]
public sealed class UsuariosController(
    IUsuarioServicioConsulta consulta,
    IUsuarioServicioComandos comandos,
    IRolServicioConsulta roles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await consulta.ListAsync(cancellationToken));
    }

    [HttpGet("consulta")]
    [ProducesResponseType(typeof(PagedResult<UsuarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<UsuarioDto>>> GetConsulta(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery(Name = "size")] int? size = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(size ?? pageSize, 1, 100);
        var segmento = string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? UsuarioSegmentoListado.Eliminadas
            : UsuarioSegmentoListado.Activas;
        var query = new UsuarioListQuery(
            normalizedPage,
            normalizedPageSize,
            search,
            sort,
            segmento);

        return Ok(await consulta.QueryAsync(query, cancellationToken));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        var user = await consulta.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("roles")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRoles(
        CancellationToken cancellationToken)
    {
        return Ok(await roles.ListAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Create(
        CrearUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await comandos.CrearAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Update(
        string id,
        ActualizarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await comandos.ActualizarAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await comandos.DesactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    [HttpPatch("{id}/reactivar")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Reactivate(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await comandos.ReactivarAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }

    [HttpPut("{userId}/roles")]
    [Authorize(Roles = RolesSgv.Administrador)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> AssignRoles(
        string userId,
        AsignarRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await comandos.AsignarRolesAsync(userId, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ApiResults.ToProblemResult(result.Error!, HttpContext);
    }
}
