using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Api.Controllers;

[ApiController]
[Route("api/v1/tipos-unidad-organizativa")]
public class TipoUnidadesOrganizativasController : ControllerBase
{
    private readonly ITipoUnidadOrganizativaServicioConsulta _servicio;

    public TipoUnidadesOrganizativasController(ITipoUnidadOrganizativaServicioConsulta servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TipoUnidadOrganizativaDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TipoUnidadOrganizativaDto>> GetById(
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
