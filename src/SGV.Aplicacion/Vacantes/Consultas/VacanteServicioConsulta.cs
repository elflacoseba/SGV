using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Dominio.Vacantes;

namespace SGV.Aplicacion.Vacantes.Consultas;

/// <summary>
/// Default read-only service for <see cref="Vacante"/>. Delegates
/// filtering/pagination/segmentation to the repository and maps each
/// domain aggregate to the consumer-safe
/// <see cref="VacanteDto"/>/<see cref="VacanteDetailDto"/> wire-types.
/// <c>Puesto.Nombre</c> and <c>EstadoVacante.Nombre</c> are denormalised
/// here so the response is self-contained and the client doesn't need
/// to round-trip to the catalog endpoints.
/// </summary>
public sealed class VacanteServicioConsulta(
        IVacanteRepository repository,
        IOcupacionRepository ocupacionRepository)
    : IVacanteServicioConsulta
{
    public async Task<PagedResult<VacanteDto>> ListarAsync(
        VacanteListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, totalCount) = await repository
            .ListarAsync(query, cancellationToken)
            .ConfigureAwait(false);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<VacanteDto>(dtos, totalCount, query.Page, query.PageSize);
    }

    public async Task<VacanteDetailDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // GetByIdForUpdateAsync loads the domain with eager-loaded
        // HistorialEstados (ThenInclude EstadoAnterior/Nuevo), so the
        // detail DTO can serialise the full history. It also filters
        // !IsDeleted which matches the spec's "404 for non-existent".
        var vacante = await repository
            .GetByIdForUpdateAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (vacante is null)
        {
            return null;
        }

        // D-3 (invertir-flujo-cubrir): hidratar OcupacionDerivadaId y
        // PersonaAsignadaNombre desde IOcupacionRepository sólo cuando la
        // Vacante está Cubierta. Para Vacantes no Cubiertas el round-trip
        // extra no aplica (defensivo + ahorro de query). El path defensivo
        // "Cubierta sin Ocupacion" retorna ambos campos null sin lanzar.
        Guid? ocupacionDerivadaId = null;
        string? personaAsignadaNombre = null;
        if (vacante.EstadoVacante?.EsCubierta == true)
        {
            var cobertura = await ocupacionRepository
                .ObtenerVigentePorVacanteAsync(vacante.Id, cancellationToken)
                .ConfigureAwait(false);
            if (cobertura is { } c)
            {
                ocupacionDerivadaId = c.Id;
                personaAsignadaNombre = c.PersonaNombre;
            }
        }

        return MapToDetailDto(vacante, ocupacionDerivadaId, personaAsignadaNombre);
    }

    private static VacanteDto MapToDto(Vacante vacante)
    {
        return new VacanteDto(
            vacante.Id,
            vacante.PuestoId,
            vacante.Puesto?.Nombre ?? string.Empty,
            vacante.EstadoVacanteId,
            vacante.EstadoVacante?.Nombre ?? string.Empty,
            vacante.FechaApertura,
            vacante.FechaCierre,
            vacante.Motivo,
            vacante.Observaciones);
    }

    private static VacanteDetailDto MapToDetailDto(
        Vacante vacante,
        Guid? ocupacionDerivadaId,
        string? personaAsignadaNombre)
    {
        var historial = vacante.HistorialEstados
            .Select(h => new HistorialEstadoVacanteDto(
                EstadoAnteriorNombre: h.EstadoAnterior?.Nombre,
                EstadoNuevoNombre: h.EstadoNuevo?.Nombre ?? string.Empty,
                ChangedAt: h.ChangedAt,
                ChangedByUserId: h.ChangedByUserId,
                Motivo: h.Motivo))
            .ToArray();

        return new VacanteDetailDto(
            vacante.Id,
            vacante.PuestoId,
            vacante.Puesto?.Nombre ?? string.Empty,
            vacante.EstadoVacanteId,
            vacante.EstadoVacante?.Nombre ?? string.Empty,
            vacante.FechaApertura,
            vacante.FechaCierre,
            vacante.Motivo,
            vacante.Observaciones,
            historial,
            ocupacionDerivadaId,
            personaAsignadaNombre);
    }
}
