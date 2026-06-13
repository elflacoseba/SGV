using SGV.Dominio.Comun;
using SGV.Dominio.Personas;

namespace SGV.Dominio.Seleccion;

public sealed class Postulante : EntidadAuditable
{
    private readonly List<Postulacion> _postulaciones = [];

    private Postulante()
    {
    }

    public Postulante(Guid? personaId, string? nombres, string? apellidos, string? email = null)
    {
        if (!personaId.HasValue && (string.IsNullOrWhiteSpace(nombres) || string.IsNullOrWhiteSpace(apellidos)))
        {
            throw new InvalidOperationException("Un postulante externo debe tener nombres y apellidos.");
        }

        PersonaId = personaId;
        Nombres = ValidacionesDominio.Opcional(nombres, nameof(Nombres), 100);
        Apellidos = ValidacionesDominio.Opcional(apellidos, nameof(Apellidos), 100);
        Email = ValidacionesDominio.Opcional(email, nameof(Email), 320);
    }

    public Guid? PersonaId { get; private set; }

    public Persona? Persona { get; private set; }

    public string? Nombres { get; private set; }

    public string? Apellidos { get; private set; }

    public string? Email { get; private set; }

    public string? Telefono { get; private set; }

    public string? Fuente { get; private set; }

    public string? Observaciones { get; private set; }

    public IReadOnlyCollection<Postulacion> Postulaciones => _postulaciones;
}
