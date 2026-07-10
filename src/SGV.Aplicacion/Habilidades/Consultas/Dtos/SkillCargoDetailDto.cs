using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Consultas.Dtos;

/// <summary>
/// GET-only detailed DTO for a Habilidad → Cargo association (mirror of
/// <see cref="SGV.Contracts.Organizacion.Consultas.Dtos.CargoSkillDetailDto"/>).
/// The primary constructor preserves the existing <c>(cargo, nivel)</c> shape
/// used by the EF Core projection in the infrastructure layer, while the link
/// fields (<c>cargoId</c>, <c>nivelRequeridoId</c>, <c>ponderacion</c>,
/// <c>esObligatoria</c>) are exposed as init-only properties so the
/// infrastructure projection can populate them without breaking the
/// two-argument call site.
/// </summary>
/// <remarks>
/// Skill-cargo-query-contract requirement: every item MUST expose
/// <c>CargoId</c>, <c>Codigo</c>, <c>Nombre</c>, <c>NivelId</c>,
/// <c>NivelNombre</c>, <c>CargoEliminado</c>, <c>NivelRequeridoId</c>,
/// <c>Ponderacion</c> and <c>EsObligatoria</c>. The nested <see cref="Cargo"/>
/// object exposes <c>Id</c>/<c>Codigo</c>/<c>Nombre</c>/<c>NivelId</c>/
/// <c>NivelNombre</c>; the link-level <c>CargoId</c> and
/// <c>NivelRequeridoId</c> are the authoritative values for editable
/// consumers. <c>CargoEliminado</c> mirrors the underlying <c>Cargo</c>
/// soft-delete flag (<see cref="Dominio.Comun.EntidadAuditable.IsDeleted"/>)
/// and is independent from the request segment — UI badges can render it
/// without re-querying.
/// </remarks>
public sealed record SkillCargoDetailDto(
    CargoDto Cargo,
    NivelHabilidadDto Nivel)
{
    /// <summary>
    /// Identifier of the underlying cargo on the CargoHabilidad link.
    /// Mirrors <see cref="CargoDto.Id"/>; exposed for consumers that bind to
    /// ids without navigating the nested cargo object.
    /// </summary>
    public Guid CargoId { get; init; }

    /// <summary>
    /// Required <see cref="Dominio.Habilidades.NivelHabilidad"/> identifier on
    /// the CargoHabilidad link.
    /// </summary>
    public Guid NivelRequeridoId { get; init; }

    /// <summary>
    /// Persisted weight for the link.
    /// </summary>
    public decimal Ponderacion { get; init; }

    /// <summary>
    /// Persisted mandatory flag for the link.
    /// </summary>
    public bool EsObligatoria { get; init; }

    /// <summary>
    /// Soft-delete flag for the underlying <see cref="Cargo"/>. Mirrors
    /// <see cref="Dominio.Comun.EntidadAuditable.IsDeleted"/> and is
    /// populated by the EF Core projection regardless of the request segment,
    /// so the UI can render a "dado de baja" badge without re-querying.
    /// </summary>
    public bool CargoEliminado { get; init; }
}