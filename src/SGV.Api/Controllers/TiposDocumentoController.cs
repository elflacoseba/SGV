using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// Read-only catalog queries for <c>TipoDocumento</c> (issue #147).
/// Sigue el patrón de <see cref="NivelesCargoController"/>: autenticación
/// default-deny, sólo GET ⇒ 405 natural para POST/PUT/PATCH/DELETE.
/// </summary>
[ApiController]
[Route("api/v1/tipos-documento")]
[Produces("application/json")]
[Authorize]
public class TiposDocumentoController : ControllerBase
{
    private readonly ITipoDocumentoCatalogoConsulta _servicio;

    public TiposDocumentoController(ITipoDocumentoCatalogoConsulta servicio)
    {
        _servicio = servicio;
    }

    /// <summary>
    /// Lista todos los tipos de documento del catálogo (4 filas seed: DNI/LE/LC/Pasaporte).
    /// Issue #195: <c>[AllowAnonymous]</c> en este endpoint para que el
    /// formulario de setup inicial pueda cargar el dropdown de
    /// <c>TipoDocumento</c> sin requerir un admin logueado
    /// (chicken-and-egg). El resto del controller (<c>GetById</c>)
    /// mantiene la autorización heredada de <c>[Authorize]</c> a nivel clase.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Lista de tipos de documento.</returns>
    /// <response code="200">Lista devuelta correctamente.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<TipoDocumentoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TipoDocumentoDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListarAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene un tipo de documento puntual por su identificador.
    /// </summary>
    /// <param name="id">Identificador único del tipo de documento.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <returns>Tipo de documento solicitado.</returns>
    /// <response code="200">Tipo de documento encontrado.</response>
    /// <response code="400">El identificador proporcionado no es un GUID válido.</response>
    /// <response code="404">No se encontró un tipo de documento con el ID especificado.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TipoDocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TipoDocumentoDto>> GetById(
        string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return BadRequest();
        }

        var result = await _servicio.ObtenerPorIdAsync(guid, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
