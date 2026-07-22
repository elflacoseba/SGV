using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Contracts.Habilidades.Consultas.Dtos;

/// <summary>Transportable paginated result for personas associated with a skill.</summary>
public sealed record PersonaHabilidadesPageResult(
    IReadOnlyList<SkillPersonaDetailDto> Items,
    int Page,
    int PageSize,
    int Total,
    string? Sort,
    PersonaSegmentoListado Segmento);
