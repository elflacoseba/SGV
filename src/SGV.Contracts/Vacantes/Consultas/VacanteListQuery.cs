using SGV.Contracts.Vacantes.Enums;

namespace SGV.Contracts.Vacantes.Consultas;

/// <summary>
/// Query de listado de vacantes. El segmento por defecto es
/// <see cref="VacanteSegmentoListado.Abiertas"/>.
/// </summary>
public sealed record VacanteListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    VacanteSegmentoListado Segmento = VacanteSegmentoListado.Abiertas,
    Guid? PuestoId = null);
