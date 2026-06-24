using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;
using SGV.Dominio.Ocupaciones;

namespace SGV.Aplicacion.Ocupaciones.Consultas;

/// <summary>
/// Implements read-only queries for Ocupaciones.
/// Default list returns active rows; <c>includeHistory</c> includes finalized
/// and logically deleted rows. Detail reads always include historical data.
/// </summary>
public sealed class OcupacionServicioConsulta(IOcupacionRepository repository)
    : IOcupacionServicioConsulta
{
    public async Task<IReadOnlyList<OcupacionDto>> ListAsync(
        bool includeHistory = false,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Ocupacion> entities;

        if (includeHistory)
        {
            entities = await repository.ListAllIncludingHistoryAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entities = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        }

        return entities.Select(MapToDto).ToList();
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
            ocupacion.TipoAsignacion,
            ocupacion.Observaciones,
            OcupacionEstadoHelper.CalcularEstado(ocupacion));
    }
}
