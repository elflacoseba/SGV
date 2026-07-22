using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>Repository contract for the readonly Skill to Persona subresource.</summary>
public interface ISkillPersonaRepository
{
    Task<PersonaHabilidadesPageResult> ListDetailedBySkillIdAsync(
        Guid skillId,
        HabilidadPersonasListQuery query,
        CancellationToken cancellationToken = default);
}
