using SGV.Dominio.Comun;
using SGV.Dominio.Ocupaciones;

namespace SGV.Dominio.Personas;

public sealed class Persona : EntidadAuditable
{
    private readonly List<PersonaHabilidad> _habilidades = [];
    private readonly List<Ocupacion> _ocupaciones = [];

    private Persona()
    {
    }

    public Persona(string nombres, string apellidos, string? legajo = null, string? email = null)
    {
        CambiarDatos(nombres, apellidos, legajo, email);
        IsActive = true;
    }

    public string? Legajo { get; private set; }

    public string Nombres { get; private set; } = string.Empty;

    public string Apellidos { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? TipoDocumento { get; private set; }

    public string? NumeroDocumento { get; private set; }

    public string? Telefono { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<PersonaHabilidad> Habilidades => _habilidades;

    public IReadOnlyCollection<Ocupacion> Ocupaciones => _ocupaciones;

    public void CambiarDatos(string nombres, string apellidos, string? legajo = null, string? email = null, string? telefono = null)
    {
        Nombres = ValidacionesDominio.Requerido(nombres, nameof(Nombres), 100);
        Apellidos = ValidacionesDominio.Requerido(apellidos, nameof(Apellidos), 100);
        Legajo = ValidacionesDominio.Opcional(legajo, nameof(Legajo), 50);
        Email = ValidacionesDominio.Opcional(email, nameof(Email), 320);
        Telefono = ValidacionesDominio.Opcional(telefono, nameof(Telefono), 50);
    }

    public void CambiarDocumento(string? tipoDocumento, string? numeroDocumento)
    {
        TipoDocumento = ValidacionesDominio.Opcional(tipoDocumento, nameof(TipoDocumento), 50);
        NumeroDocumento = ValidacionesDominio.Opcional(numeroDocumento, nameof(NumeroDocumento), 50);
    }

    public PersonaHabilidad AgregarHabilidad(Guid habilidadId, Guid nivelHabilidadId, DateTime? verificadoAt = null, string? fuente = null)
    {
        if (_habilidades.Any(h => h.HabilidadId == habilidadId))
        {
            throw new InvalidOperationException("La persona ya tiene registrada esa habilidad.");
        }

        var personaHabilidad = new PersonaHabilidad(Id, habilidadId, nivelHabilidadId, verificadoAt, fuente);
        _habilidades.Add(personaHabilidad);
        return personaHabilidad;
    }
}
