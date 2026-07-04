namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for a Cargo-Habilidad association returned by write
/// operations. Carries <c>skillId</c>, <c>nivelRequeridoId</c>, <c>ponderacion</c>
/// and <c>esObligatoria</c> so the web shell can rehydrate the editable table
/// without recomputing values client-side.
/// </summary>
/// <remarks>
/// The two-argument primary constructor <c>(skillId, nivelRequeridoId)</c> is
/// the single source of truth for the link identifier. New code MUST populate
/// <see cref="NivelRequeridoId"/> positionally; the controller is responsible
/// for serialising the resulting JSON using <c>nivelRequeridoId</c>.
/// </remarks>
public sealed record CargoSkillDto(
    Guid SkillId,
    Guid NivelRequeridoId)
{
    /// <summary>
    /// Persisted weight for the link. Defaults to <c>1.00</c> when constructed
    /// via the primary constructor; new code should set the actual persisted
    /// value.
    /// </summary>
    public decimal Ponderacion { get; init; } = 1.00m;

    /// <summary>
    /// Persisted mandatory flag for the link. Defaults to <c>false</c> when
    /// constructed via the primary constructor; new code should set the actual
    /// persisted value.
    /// </summary>
    public bool EsObligatoria { get; init; }
}