namespace SGV.Contracts.Personas.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for the <c>TipoDocumento</c> catalog. Mirrors the shape
/// of the seed rows declared in <c>TipoDocumentoConstantes</c> so that the
/// HTTP round-trip preserves the regex pattern (one <c>\</c> on the wire,
/// escaped to two <c>\\</c> in JSON).
/// </summary>
public sealed record TipoDocumentoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? PatronValidacion,
    int? LongitudMinima,
    int? LongitudMaxima);
