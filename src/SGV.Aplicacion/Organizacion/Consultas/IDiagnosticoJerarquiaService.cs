namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Represents a single hierarchy cycle detected in
/// <c>UnidadesOrganizativas</c>. Each entry lists the node ids that
/// participate in the cycle in traversal order so the operator can locate
/// the offending rows in the persistence layer.
/// </summary>
/// <remarks>
/// The path always includes the closure node (first node == last node) so
/// the wire consumer can render the loop visually: <c>A → B → A</c>
/// reports <c>[A.Id, B.Id, A.Id]</c>.
/// </remarks>
public sealed record CicloDetectado(IReadOnlyList<Guid> Nodos);

/// <summary>
/// Detects pre-existing cycles in the <c>UnidadesOrganizativas</c>
/// hierarchy. The diagnostic MUST report detected cycles without mutating
/// any row and MUST return an empty list when the tree is acyclic.
/// </summary>
public interface IDiagnosticoJerarquiaService
{
    /// <summary>
    /// Performs a one-shot pass over the active organizational units and
    /// returns the list of detected cycles.
    /// </summary>
    Task<IReadOnlyList<CicloDetectado>> DiagnosticarAsync(CancellationToken cancellationToken = default);
}
