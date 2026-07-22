using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>Validates the parent skill and delegates its persona query.</summary>
public sealed class SkillPersonaServicioConsulta(
    ISkillPersonaRepository repository,
    IHabilidadServicioConsulta habilidadServicio) : ISkillPersonaServicioConsulta
{
    public async Task<PersonaHabilidadesPageResult?> ListarPersonasAsync(
        Guid skillId,
        HabilidadPersonasListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (skillId == Guid.Empty)
        {
            throw new ArgumentException("The skill identifier cannot be empty.", nameof(skillId));
        }

        var habilidad = await habilidadServicio.GetByIdAsync(skillId, cancellationToken).ConfigureAwait(false);
        if (habilidad is null)
        {
            return null;
        }

        return await repository.ListDetailedBySkillIdAsync(skillId, query, cancellationToken).ConfigureAwait(false);
    }
}
