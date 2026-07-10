using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Contracts.Organizacion.Consultas.Dtos;

/// <summary>
/// GET-only detailed DTO for a Cargo-Habilidad association. The primary
/// constructor preserves the existing <c>(skill, nivel)</c> shape used by
/// the EF Core projection in the infrastructure layer, while the new link
/// fields (<c>skillId</c>, <c>nivelRequeridoId</c>, <c>ponderacion</c>,
/// <c>esObligatoria</c>) are exposed as init-only properties so the
/// infrastructure projection can populate them without breaking the
/// two-argument call site.
/// </summary>
public sealed record CargoSkillDetailDto(
    HabilidadDto Skill,
    NivelHabilidadDto Nivel)
{
    /// <summary>
    /// Identifier of the underlying skill. Mirrors <see cref="HabilidadDto.Id"/>;
    /// exposed for editable tables that bind to ids.
    /// </summary>
    public Guid SkillId { get; init; }

    /// <summary>
    /// Required <see cref="Dominio.Habilidades.NivelHabilidad"/> identifier on the
    /// CargoHabilidad link.
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
}
