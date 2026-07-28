using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Ocupaciones;

namespace SGV.Aplicacion.Ocupaciones.Consultas;

/// <summary>
/// Implements read-only queries for Ocupaciones.
/// Lists are segmented and filtered server-side; detail reads include historical data.
/// </summary>
public sealed class OcupacionServicioConsulta(IOcupacionRepository repository)
    : IOcupacionServicioConsulta
{
    public async Task<PagedResult<OcupacionDto>> QueryAsync(
        OcupacionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository
            .QueryAsync(query, cancellationToken)
            .ConfigureAwait(false);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<OcupacionDto>(dtos, totalCount, query.Page, query.PageSize);
    }

    public async Task<OcupacionDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Detail reads bypass soft-delete filters to always return historical data.
        var entity = await repository.GetByIdIncludingHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        return entity is not null ? MapToDto(entity) : null;
    }

    private static OcupacionDto MapToDto(Ocupacion ocupacion)
    {
        var personaNombre = ocupacion.Persona is not null
            ? $"{ocupacion.Persona.Nombres} {ocupacion.Persona.Apellidos}"
            : "";
        var puestoNombre = ocupacion.Puesto?.Nombre ?? "";

        return new OcupacionDto(
            ocupacion.Id,
            ocupacion.PersonaId,
            personaNombre,
            ocupacion.PuestoId,
            puestoNombre,
            ocupacion.FechaInicio,
            ocupacion.FechaFin,
            (OcupacionTipoAsignacion)ocupacion.TipoAsignacion,
            ocupacion.Observaciones,
            OcupacionEstadoHelper.CalcularEstado(ocupacion));
    }
}
