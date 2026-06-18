using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// Read-only catalog queries for NivelCargo.
/// </summary>
[ApiController]
[Route("api/v1/niveles-cargo")]
[Produces("application/json")]
public class NivelesCargoController : ControllerBase
{
    private readonly INivelCargoServicioConsulta _servicio;

    public NivelesCargoController(INivelCargoServicioConsulta servicio)
    {
        _servicio = servicio;
    }

    /// <summary>
    /// Obtiene todos los niveles de cargo del catálogo.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de niveles de cargo.</returns>
    /// <response code="200">Lista de niveles de cargo devuelta correctamente.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NivelCargoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NivelCargoDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un nivel de cargo por su identificador único.
    /// </summary>
    /// <param name="id">Identificador único del nivel de cargo.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Nivel de cargo solicitado.</returns>
    /// <response code="200">Nivel de cargo encontrado.</response>
    /// <response code="400">El identificador proporcionado no es un GUID válido.</response>
    /// <response code="404">No se encontró un nivel de cargo con el ID especificado.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(NivelCargoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NivelCargoDto>> GetById(
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
