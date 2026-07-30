using SGV.Dominio.Comun;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Seleccion;

namespace SGV.Dominio.Vacantes;

public sealed record class Vacante : EntidadAuditable
{
    private readonly List<HistorialEstadoVacante> _historialEstados = [];
    private readonly List<Postulacion> _postulaciones = [];

    private Vacante()
    {
    }

    public Vacante(Guid puestoId, Guid estadoVacanteId, DateTime fechaApertura, string motivo)
    {
        PuestoId = puestoId;
        EstadoVacanteId = estadoVacanteId;
        FechaApertura = fechaApertura;
        Motivo = ValidacionesDominio.Requerido(motivo, nameof(Motivo), 500);
    }

    public Guid PuestoId { get; private set; }

    public Puesto Puesto { get; private set; } = null!;

    public Guid EstadoVacanteId { get; private set; }

    public EstadoVacante EstadoVacante { get; private set; } = null!;

    public DateTime FechaApertura { get; private set; }

    public DateTime? FechaCierre { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public string? Observaciones { get; private set; }

    public IReadOnlyCollection<HistorialEstadoVacante> HistorialEstados => _historialEstados;

    public IReadOnlyCollection<Postulacion> Postulaciones => _postulaciones;

    public HistorialEstadoVacante CambiarEstado(Guid estadoNuevoId, string? usuarioId, string? motivo = null, DateTime? fecha = null, bool cerrar = false)
    {
        var cambio = new HistorialEstadoVacante(Id, EstadoVacanteId, estadoNuevoId, fecha ?? DateTime.UtcNow, usuarioId, motivo);
        EstadoVacanteId = estadoNuevoId;
        if (cerrar)
        {
            FechaCierre = cambio.ChangedAt;
        }

        _historialEstados.Add(cambio);
        return cambio;
    }

    /// <summary>
    /// Updates the free-form observations for this <see cref="Vacante"/>.
    /// </summary>
    /// <param name="observaciones">
    /// New observations text. <see langword="null"/>, empty or whitespace-only
    /// values clear the existing observations. Values longer than
    /// 500 characters after trimming throw <see cref="ArgumentException"/>.
    /// </param>
    public void ActualizarObservaciones(string? observaciones)
    {
        Observaciones = ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 500);
    }

    /// <summary>
    /// Factory de hidratación desde la capa de persistencia. Replica la
    /// validación de <c>Motivo</c> del constructor primario y asigna todos
    /// los campos persistibles con setters tipados (no reflexión), en
    /// paridad con <c>Puesto.Reconstitute</c> y <c>Ocupacion.Reconstitute</c>.
    /// </summary>
    /// <remarks>
    /// Orden canónico: id + audit + <c>IsDeleted</c> primero, luego los
    /// datos primarios (incluyendo <c>FechaCierre</c> y <c>Motivo</c>), y
    /// por último las nav properties. La colección
    /// <c>_historialEstados</c> queda vacía en esta fase: la reconstrucción
    /// desde la capa de persistencia se realiza vía EF tracking sobre la
    /// entidad, no sobre el agregado de dominio. El bridge entre ambos
    /// modelos es responsabilidad del servicio de comandos (work unit 3.x).
    /// </remarks>
    internal static Vacante Reconstitute(
        Guid id,
        Guid puestoId,
        Guid estadoVacanteId,
        DateTime fechaApertura,
        DateTime? fechaCierre,
        string motivo,
        string? observaciones,
        Puesto? puesto,
        EstadoVacante? estadoVacante,
        DateTime createdAt,
        string? createdByUserId,
        DateTime? updatedAt,
        string? updatedByUserId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedByUserId)
    {
        if (motivo.Length > 500)
        {
            throw new InvalidOperationException("El motivo excede los 500 caracteres.");
        }

        var self = new Vacante
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

        self.PuestoId = puestoId;
        self.EstadoVacanteId = estadoVacanteId;
        self.FechaApertura = fechaApertura;
        self.FechaCierre = fechaCierre;
        self.Motivo = motivo;
        self.Observaciones = ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 500);
        self.Puesto = puesto!;
        self.EstadoVacante = estadoVacante!;

        return self;
    }
}
