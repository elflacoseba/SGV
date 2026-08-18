using SGV.Dominio.Comun;
using SGV.Dominio.Habilidades;

namespace SGV.Dominio.Organizacion;

public sealed record class Cargo : EntidadAuditable
{
    private readonly List<CargoHabilidad> _habilidades = [];
    private readonly List<Puesto> _puestos = [];

    private Cargo()
    {
    }

    public Cargo(string codigo, string nombre, Guid nivelId, string? descripcion = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        ValidarNivelId(nivelId);
        NivelId = nivelId;
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
        IsActive = true;
    }

    /// <summary>
    /// Código único del cargo. Mutable solo desde dentro de la entidad vía
    /// <see cref="Actualizar"/>; la verificación de unicidad activa contra
    /// otros Cargos es responsabilidad del servicio de aplicación.
    /// </summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Descripcion { get; private set; }

    /// <summary>
    /// Identificador del NivelCargo asociado.
    /// </summary>
    public Guid NivelId { get; private set; }

    /// <summary>
    /// Navegación al NivelCargo asociado.
    /// </summary>
    public NivelCargo? NivelCargo { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<CargoHabilidad> Habilidades => _habilidades;

    public IReadOnlyCollection<Puesto> Puestos => _puestos;

    /// <summary>
    /// Actualiza los campos editables del cargo, incluido <see cref="Codigo"/>.
    /// La unicidad activa del código se valida en el servicio de aplicación
    /// antes de invocar este método; este solo aplica reglas de shape
    /// (requerido, longitud máxima).
    /// </summary>
    /// <param name="codigo">Nuevo código del cargo. Requerido, máximo 50 caracteres.</param>
    /// <param name="nombre">Nuevo nombre del cargo. Requerido, máximo 200 caracteres.</param>
    /// <param name="nivelId">Identificador del NivelCargo asociado.</param>
    /// <param name="descripcion">Descripción opcional, máximo 1000 caracteres.</param>
    public void Actualizar(string codigo, string nombre, Guid nivelId, string? descripcion = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        ValidarNivelId(nivelId);
        NivelId = nivelId;
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }

    /// <summary>
    /// Desactiva el cargo. La verificación autoritativa de la invariante
    /// "no desactivar un cargo con Puestos subordinados activos" vive en
    /// <c>CargoServicioComandos.DesactivarAsync</c>, que consulta la base
    /// de datos vía <c>ICargoRepository.HasActivePuestosAsync</c> antes de
    /// invocar este método.
    /// <para>
    /// El chequeo local sobre <see cref="_puestos"/> que aparece acá es una
    /// defensa secundaria: solo se evalúa si la navegación a <see cref="Puestos"/>
    /// fue cargada explícitamente por el caller (e.g. vía
    /// <c>Include(c =&gt; c.Puestos)</c>). En el camino de producción, la
    /// navegación NO se carga, por lo que este bloque es dead code en runtime
    /// pero sirve como guard en memoria cuando alguien rehidrata la entidad
    /// con la nav incluida y luego invoca <c>Desactivar()</c> directamente
    /// sin pasar por el servicio. No es la regla de negocio autoritativa;
    /// confiar siempre en el chequeo del servicio para evitar desactivaciones
    /// inválidas.
    /// </para>
    /// </summary>
    public void Desactivar()
    {
        if (_puestos.Count > 0 && _puestos.Any(p => p.IsActive))
        {
            throw new InvalidOperationException(
                "No se puede desactivar el cargo porque tiene Puestos activos asociados.");
        }

        IsActive = false;
    }

    /// <summary>
    /// Reactiva el cargo. La verificación de unicidad de Codigo activo
    /// es responsabilidad del servicio de aplicación.
    /// </summary>
    public void Activar()
    {
        IsActive = true;
    }

    public CargoHabilidad AgregarHabilidad(Guid habilidadId, Guid nivelRequeridoId, decimal ponderacion, bool esObligatoria)
    {
        if (_habilidades.Any(h => h.HabilidadId == habilidadId))
        {
            throw new InvalidOperationException("La habilidad ya está configurada para el cargo.");
        }

        var cargoHabilidad = new CargoHabilidad(Id, habilidadId, nivelRequeridoId, ponderacion, esObligatoria);
        _habilidades.Add(cargoHabilidad);
        return cargoHabilidad;
    }

    private static void ValidarNivelId(Guid nivelId)
    {
        if (nivelId == Guid.Empty)
        {
            throw new ArgumentException("El nivel de cargo es obligatorio.", nameof(NivelId));
        }
    }

    /// <summary>
    /// Factory de hidratación desde la capa de persistencia. Asigna todos los
    /// campos persistibles (incluyendo audit + <see cref="IsActive"/> y la nav
    /// <see cref="NivelCargo"/>) con setters tipados para evitar el path de
    /// reflexión que <c>PersistenceToDomainMapper.SetProperty</c> implementaba.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Importante:</b> <paramref name="isActive"/> se asigna directamente
    /// como flag sin invocar <see cref="Desactivar"/>, por lo que la invariante
    /// "no desactivar un cargo con puestos subordinados activos" <b>no se
    /// evalúa aquí</b>. Esta es la misma semántica que el path de reflexión
    /// previo; un cargo persistido con <c>IsActive=false</c> pero con puestos
    /// subordinados cargados se rehidrata sin lanzar. Endurecer esta
    /// invariante queda fuera de scope de este change (ver
    /// <c>archive-report</c>).
    /// </para>
    /// </remarks>
    internal static Cargo Reconstitute(
        Guid id,
        string codigo,
        string nombre,
        Guid nivelId,
        string? descripcion,
        bool isActive,
        NivelCargo? nivelCargo,
        DateTime createdAt,
        string? createdByUserId,
        DateTime? updatedAt,
        string? updatedByUserId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedByUserId)
    {
        ValidarNivelId(nivelId);

        var self = new Cargo
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

        self.Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        self.Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        self.NivelId = nivelId;
        self.Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
        self.IsActive = isActive;
        self.NivelCargo = nivelCargo;

        return self;
    }
}
