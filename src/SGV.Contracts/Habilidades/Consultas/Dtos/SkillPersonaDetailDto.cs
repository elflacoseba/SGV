using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Contracts.Habilidades.Consultas.Dtos;

/// <summary>Details of a persona associated with a skill.</summary>
public sealed record SkillPersonaDetailDto(PersonaDto Persona, NivelHabilidadDto Nivel)
{
    public Guid PersonaId { get; init; }
    public Guid HabilidadId { get; init; }
    public Guid NivelHabilidadId { get; init; }
}
