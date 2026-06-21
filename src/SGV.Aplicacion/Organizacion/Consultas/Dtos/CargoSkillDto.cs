namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for a Cargo-Habilidad association.
/// Exposes <c>skillId</c> and <c>nivelId</c> per the consumer contract,
/// hiding internal property names (NivelRequeridoId).
/// </summary>
public sealed record CargoSkillDto(
    Guid SkillId,
    Guid NivelId
);
