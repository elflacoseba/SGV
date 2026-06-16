using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Api.Controllers;

[ApiController]
[Route("api/v1/unidades-organizativas")]
public class UnidadesOrganizativasController : ControllerBase
{
    private readonly IUnidadOrganizativaServicioConsulta _servicio;
    private readonly IUnidadOrganizativaServicioComandos _comandos;

    public UnidadesOrganizativasController(
        IUnidadOrganizativaServicioConsulta servicio,
        IUnidadOrganizativaServicioComandos comandos)
    {
        _servicio = servicio;
        _comandos = comandos;
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

    /// <summary>
    /// Creates a new organizational unit.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UnidadOrganizativaDto>> Create(
        CrearUnidadOrganizativaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.CrearAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Updates an existing organizational unit.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UnidadOrganizativaDto>> Update(
        Guid id,
        ActualizarUnidadOrganizativaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.ActualizarAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Changes the parent of an organizational unit.
    /// </summary>
    [HttpPatch("{id:guid}/unidad-padre")]
    public async Task<ActionResult<UnidadOrganizativaDto>> ChangeParent(
        Guid id,
        CambiarUnidadPadreRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.CambiarUnidadPadreAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// Soft-deletes an organizational unit.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _comandos.EliminarAsync(id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : ToProblemResult(result.Error!);
    }

    private ActionResult ToProblemResult(UnidadOrganizativaError error)
    {
        var statusCode = error.Type switch
        {
            UnidadOrganizativaErrorType.NotFound => StatusCodes.Status404NotFound,
            UnidadOrganizativaErrorType.Conflict => StatusCodes.Status409Conflict,
            UnidadOrganizativaErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Message,
            type: $"https://httpstatuses.com/{statusCode}");
    }
}
