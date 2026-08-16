namespace SGV.Contracts.Organizacion.Consultas.Dtos;

/// <summary>
/// Response wrapper for <c>GET /api/v1/unidades-organizativas/arbol</c>.
/// Carries the partial hierarchy (excluding nodes that would create cycles
/// in the dataset) alongside the list of ids whose padre chain participates
/// in at least one cycle so the caller can surface them.
/// </summary>
/// <remarks>
/// Issue #277 / spec <c>unidad-organizativa-crud</c> "Construcción del
/// árbol nunca crashea ante ciclos" — the API now reports nodes involved
/// in cycles instead of silently dropping them or risking a
/// <c>StackOverflowException</c>.
/// </remarks>
public sealed record UnidadOrganizativaArbolResponse(
    IReadOnlyList<UnidadOrganizativaTreeNodeDto> Arbol,
    IReadOnlyList<Guid> NodosConCiloDetectado);
