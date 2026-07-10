using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Default implementation of <see cref="ISkillCargoServicioConsulta"/>. Acts
/// as a thin pass-through: the controller already normalizes
/// <c>page</c>/<c>pageSize</c>/<c>status</c>, so this service only delegates
/// to <see cref="ISkillCargoRepository"/> and wraps the result in a
/// <see cref="PagedResult{T}"/>.
/// </summary>
public sealed class SkillCargoServicioConsulta(ISkillCargoRepository repository)
    : ISkillCargoServicioConsulta
{
    public async Task<PagedResult<SkillCargoDetailDto>> ListarCargosAsync(
        Guid habilidadId,
        HabilidadCargosListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository
            .ListDetailedBySkillIdAsync(habilidadId, query, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<SkillCargoDetailDto>(items, totalCount, query.Page, query.PageSize);
    }
}