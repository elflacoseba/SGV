using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Contracts.Habilidades.Consultas.Dtos;

/// <summary>Pagination and filtering parameters for personas associated with a skill.</summary>
public sealed record HabilidadPersonasListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    PersonaSegmentoListado Segmento = PersonaSegmentoListado.Activas);
