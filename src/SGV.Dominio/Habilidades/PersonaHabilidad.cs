using SGV.Dominio.Comun;
using SGV.Dominio.Personas;

namespace SGV.Dominio.Habilidades;

/// <summary>
/// Join entity que asocia una <see cref="Persona"/> con una
/// <see cref="Habilidad"/> y un <see cref="NivelHabilidad"/> (nivel poseído
/// por la persona, no nivel requerido del cargo).
/// </summary>
/// <remarks>
/// Vive en el bounded context <c>SGV.Dominio.Habilidades</c> (issue #311)
/// junto con su par <see cref="CargoHabilidad"/>, con quien comparte el
/// hecho de ser una entidad de asociación hacia <see cref="Habilidad"/>.
/// La asimetría original (Persona en <c>SGV.Dominio.Personas</c>,
/// Cargo en <c>SGV.Dominio.Habilidades</c>) quedaba documentada en
/// el issue #298 como fuera de scope del PR #299.
/// La decisión arquitectónica de reubicación está consolidada en
/// <c>docs/decisiones-implementacion.md</c>.
/// </remarks>
public sealed record class PersonaHabilidad : EntidadBase
{
    private PersonaHabilidad()
    {
    }

    public PersonaHabilidad(Guid personaId, Guid habilidadId, Guid nivelHabilidadId, DateTime? verificadoAt = null, string? fuente = null)
    {
        PersonaId = personaId;
        HabilidadId = habilidadId;
        NivelHabilidadId = nivelHabilidadId;
        VerificadoAt = verificadoAt;
        Fuente = ValidacionesDominio.Opcional(fuente, nameof(Fuente), 100);
    }

    public Guid PersonaId { get; private set; }

    public Persona Persona { get; private set; } = null!;

    public Guid HabilidadId { get; private set; }

    public Habilidad Habilidad { get; private set; } = null!;

    public Guid NivelHabilidadId { get; private set; }

    public NivelHabilidad NivelHabilidad { get; private set; } = null!;

    public DateTime? VerificadoAt { get; private set; }

    public string? Fuente { get; private set; }
}
