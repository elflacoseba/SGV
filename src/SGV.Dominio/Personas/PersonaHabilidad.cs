using SGV.Dominio.Comun;
using SGV.Dominio.Habilidades;

namespace SGV.Dominio.Personas;

public sealed class PersonaHabilidad : EntidadBase
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
