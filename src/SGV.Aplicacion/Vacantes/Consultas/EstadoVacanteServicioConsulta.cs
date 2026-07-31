using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Dominio.Vacantes;

namespace SGV.Aplicacion.Vacantes.Consultas;

/// <summary>
/// Default read-only service for the <c>EstadoVacante</c> catalog.
/// Delegates persistence to <see cref="IEstadoVacanteRepository"/> and
/// maps the domain aggregate to the consumer-safe
/// <see cref="EstadoVacanteDto"/> wire-type.
/// </summary>
public sealed class EstadoVacanteServicioConsulta(IEstadoVacanteRepository repository)
    : IEstadoVacanteServicioConsulta
{
    public async Task<IReadOnlyList<EstadoVacanteDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var estados = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return estados.Select(MapToDto).ToArray();
    }

    private static EstadoVacanteDto MapToDto(EstadoVacante estado)
    {
        return new EstadoVacanteDto(
            estado.Id,
            estado.Codigo,
            estado.Nombre,
            estado.Orden,
            estado.EsTerminal);
    }
}