using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Categorias.Consultas;

namespace SGV.Api.Controllers;

/// <summary>
/// Read-only catalog queries for <c>CategoriaHabilidad</c> (issue
/// migrar-campo-categoria-habilidades-a-tabla).
///
/// Sigue el patrón de <see cref="NivelesCargoController"/> y
/// <see cref="TiposDocumentoController"/> (issue #147): autenticación
/// default-deny, sólo GET ⇒ 405 natural para POST/PUT/PATCH/DELETE.
/// </summary>
[ApiController]
[Route("api/v1/categorias-habilidad")]
[Produces("application/json")]
[Authorize]
public class CategoriasHabilidadController : ControllerBase
{
    private readonly ICategoriaHabilidadServicioConsulta _servicio;

    public CategoriasHabilidadController(ICategoriaHabilidadServicioConsulta servicio)
    {
        _servicio = servicio;
    }

    /// <summary>
    /// Lista todas las categorías de habilidad del catálogo
    /// (4 filas seed: Conducción / Técnica / Dominio / Académica),
    /// ordenadas por <c>Nombre</c> asc.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de categorías.</returns>
    /// <response code="200">Lista devuelta correctamente.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoriaHabilidadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CategoriaHabilidadDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene una categoría de habilidad puntual por su identificador.
    /// </summary>
    /// <param name="id">Identificador único de la categoría.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Categoría solicitada.</returns>
    /// <response code="200">Categoría encontrada.</response>
    /// <response code="400">El identificador proporcionado no es un GUID válido.</response>
    /// <response code="404">No se encontró una categoría con el ID especificado.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoriaHabilidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoriaHabilidadDto>> GetById(
        string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return BadRequest();
        }

        var result = await _servicio.GetByIdAsync(guid, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}