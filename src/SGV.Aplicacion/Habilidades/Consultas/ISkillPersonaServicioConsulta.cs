using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>Read-only query service for personas associated with a skill.</summary>
public interface ISkillPersonaServicioConsulta
{
    Task<PersonaHabilidadesPageResult?> ListarPersonasAsync(
        Guid skillId,
        HabilidadPersonasListQuery query,
        CancellationToken cancellationToken = default);
}
