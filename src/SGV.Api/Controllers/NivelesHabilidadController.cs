using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// Read-only catalog queries for NivelHabilidad.
/// Parallel structure to <see cref="NivelesCargoController"/>.
/// </summary>
[ApiController]
[Route("api/v1/niveles-habilidad")]
[Produces("application/json")]
[Authorize]
public class NivelesHabilidadController : ControllerBase
{
    private readonly INivelHabilidadServicioConsulta _servicio;

    public NivelesHabilidadController(INivelHabilidadServicioConsulta servicio)
    {
        _servicio = servicio;
    }

    /// <summary>
    /// Obtiene todos los niveles de habilidad del catálogo.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de niveles de habilidad.</returns>
    /// <response code="200">Lista de niveles de habilidad devuelta correctamente.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NivelHabilidadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<NivelHabilidadDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un nivel de habilidad por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único del nivel de habilidad.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Nivel de habilidad solicitado.</returns>
    /// <response code="200">Nivel de habilidad encontrado.</response>
    /// <response code="400">El identificador proporcionado no es un GUID válido.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    /// <response code="404">No se encontró un nivel de habilidad con el ID especificado.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(NivelHabilidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NivelHabilidadDto>> GetById(
        string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guid))
            return BadRequest();

        var result = await _servicio.GetByIdAsync(guid, cancellationToken);
        if (result is null)
            return NotFound();

        return Ok(result);
    }
}