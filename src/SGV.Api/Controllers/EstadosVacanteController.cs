using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Vacantes;
using SGV.Contracts.Vacantes.Consultas.Dtos;

namespace SGV.Api.Controllers;

/// <summary>
/// Read-only catalog endpoint for the <c>EstadoVacante</c> catalog.
/// Pattern parity with <see cref="NivelesCargoController"/> /
/// <see cref="CategoriasHabilidadController"/>: authentication
/// required, no role restriction, returns the 4 seed states ordered
/// by <c>Orden</c> ascending.
/// </summary>
[ApiController]
[Route(VacanteApiRoutes.EstadosVacanteBase)]
[Produces("application/json")]
[Authorize]
public class EstadosVacanteController : ControllerBase
{
    private readonly IEstadoVacanteServicioConsulta _servicio;

    public EstadosVacanteController(IEstadoVacanteServicioConsulta servicio)
    {
        _servicio = servicio;
    }

    /// <summary>
    /// Devuelve los 4 estados del catálogo ordenados por <c>Orden</c>.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    /// <response code="200">Catálogo devuelto correctamente.</response>
    /// <response code="401">El consumidor no está autenticado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EstadoVacanteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EstadoVacanteDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListarAsync(cancellationToken);
        return Ok(result);
    }
}