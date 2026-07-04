namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for a Cargo-Habilidad association returned by write
/// operations. Carries <c>skillId</c>, <c>nivelId</c> (the historical level
/// identifier exposed for compatibility with existing payloads) and the
/// link-level <c>nivelRequeridoId</c>, <c>ponderacion</c> and
/// <c>esObligatoria</c> fields so the web shell can rehydrate the editable
/// table without recomputing values client-side.
/// </summary>
/// <remarks>
/// <para>The two-argument primary constructor preserves source-level
/// compatibility with callers that only know about the legacy
/// <c>(skillId, nivelId)</c> shape; new code should populate the additional
/// properties explicitly.</para>
/// <para>The <c>nivelId</c> positional parameter is kept as a transitional
/// alias for <see cref="NivelRequeridoId"/> so callers can continue to write
/// <c>new CargoSkillDto(skillId, nivelId)</c> while still surfacing the new
/// explicit identifier on the read side. The controller is responsible for
/// serialising the resulting JSON using <c>nivelRequeridoId</c>.</para>
/// </remarks>
public sealed record CargoSkillDto(
    Guid SkillId,
    Guid NivelId)
{
    /// <summary>
    /// Required <see cref="Dominio.Habilidades.NivelHabilidad"/> identifier on the
    /// CargoHabilidad link. Exposed alongside <see cref="NivelId"/> for the
    /// explicit contract expected by the web shell; defaults to the value of
    /// <see cref="NivelId"/> when constructed via the primary constructor.
    /// </summary>
    public Guid NivelRequeridoId { get; init; } = NivelId;

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