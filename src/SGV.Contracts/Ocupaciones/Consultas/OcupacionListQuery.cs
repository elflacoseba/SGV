using SGV.Contracts.Ocupaciones.Enums;

namespace SGV.Contracts.Ocupaciones.Consultas;

public sealed record OcupacionListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    OcupacionSegmentoListado Segmento = OcupacionSegmentoListado.Activas,
    Guid? PersonaId = null,
    Guid? PuestoId = null);
