using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Api.Controllers;

[ApiController]
[Route("api/v1/unidades-organizativas")]
public class UnidadesOrganizativasController : ControllerBase
{
    private readonly IUnidadOrganizativaServicioConsulta _servicio;

    public UnidadesOrganizativasController(IUnidadOrganizativaServicioConsulta servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UnidadOrganizativaDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _servicio.ListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UnidadOrganizativaDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _servicio.GetByIdAsync(id, cancellationToken);
        if (result is null)
            return NotFound();
        return Ok(result);
    }
}
