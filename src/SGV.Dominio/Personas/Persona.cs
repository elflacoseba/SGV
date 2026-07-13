using SGV.Dominio.Comun;
using SGV.Dominio.Ocupaciones;

namespace SGV.Dominio.Personas;

public sealed record class Persona : EntidadAuditable
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

    /// <summary>
    /// Desactiva la persona (baja lógica). No elimina el registro y no
    /// altera habilidades ni ocupaciones existentes.
    /// </summary>
    public void Desactivar()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reactiva la persona. La verificación de unicidad activa de Legajo, Email y documento
    /// es responsabilidad del servicio de aplicación.
    /// </summary>
    public void Activar()
    {
        IsActive = true;
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

    /// <summary>
    /// Factory de hidratación desde la capa de persistencia. Acepta los tres
    /// campos de documento (<see cref="TipoDocumento"/>, <see cref="NumeroDocumento"/>
    /// y <see cref="Telefono"/>) como parámetros explícitos para que el mapper
    /// no necesite setter externo: los asigna vía <c>private set</c> en el
    /// orden canónico. No toca las colecciones internas
    /// <c>_habilidades</c>/<c>_ocupaciones</c> — esas se hidratan por los
    /// repositorios a través de los métodos de negocio.
    /// </summary>
    internal static Persona Reconstitute(
        Guid id,
        string nombres,
        string apellidos,
        string? legajo,
        string? email,
        string? tipoDocumento,
        string? numeroDocumento,
        string? telefono,
        bool isActive,
        DateTime createdAt,
        string? createdByUserId,
        DateTime? updatedAt,
        string? updatedByUserId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedByUserId)
    {
        var self = new Persona
        {
            Id = id,
            CreatedAt = createdAt,
            CreatedByUserId = createdByUserId,
            UpdatedAt = updatedAt,
            UpdatedByUserId = updatedByUserId,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedByUserId = deletedByUserId
        };

        self.Nombres = ValidacionesDominio.Requerido(nombres, nameof(Nombres), 100);
        self.Apellidos = ValidacionesDominio.Requerido(apellidos, nameof(Apellidos), 100);
        self.Legajo = ValidacionesDominio.Opcional(legajo, nameof(Legajo), 50);
        self.Email = ValidacionesDominio.Opcional(email, nameof(Email), 320);
        self.TipoDocumento = ValidacionesDominio.Opcional(tipoDocumento, nameof(TipoDocumento), 50);
        self.NumeroDocumento = ValidacionesDominio.Opcional(numeroDocumento, nameof(NumeroDocumento), 50);
        self.Telefono = ValidacionesDominio.Opcional(telefono, nameof(Telefono), 50);
        self.IsActive = isActive;

        return self;
    }
}
