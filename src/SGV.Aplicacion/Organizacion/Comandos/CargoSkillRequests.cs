namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Request to assign or update a required skill (Habilidad) for a Cargo.
/// The <c>skillId</c> is passed as a route parameter; this payload carries
/// the link-level fields: required level, optional weight and optional
/// obligation flag.
/// </summary>
/// <param name="NivelRequeridoId">Required <see cref="Dominio.Habilidades.NivelHabilidad"/> identifier.</param>
/// <param name="Ponderacion">Optional weight assigned to the link. When null, the service falls back to the documented default (1.00).</param>
/// <param name="EsObligatoria">Optional flag indicating whether the skill is mandatory. When null, the service falls back to the documented default (false).</param>
public sealed record AsignarCargoSkillRequest(
    Guid NivelRequeridoId,
    decimal? Ponderacion = null,
    bool? EsObligatoria = null
);