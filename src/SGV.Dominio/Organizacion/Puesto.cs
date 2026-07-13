using SGV.Dominio.Comun;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Vacantes;

namespace SGV.Dominio.Organizacion;

public sealed record class Puesto : EntidadAuditable
{
    private readonly List<Ocupacion> _ocupaciones = [];
    private readonly List<Vacante> _vacantes = [];
    private readonly List<Puesto> _puestosSubordinados = [];

    private Puesto()
    {
    }

    public Puesto(Guid unidadOrganizativaId, Guid cargoId, string codigo, string nombre, Guid? puestoSuperiorId = null, string? descripcion = null)
    {
        if (unidadOrganizativaId == Guid.Empty)
            throw new ArgumentException("La unidad organizativa es obligatoria.", nameof(UnidadOrganizativaId));
        if (cargoId == Guid.Empty)
            throw new ArgumentException("El cargo es obligatorio.", nameof(CargoId));

        UnidadOrganizativaId = unidadOrganizativaId;
        CargoId = cargoId;
        CambiarDatos(codigo, nombre, descripcion);
        CambiarPuestoSuperior(puestoSuperiorId);
        IsActive = true;
    }

    public Guid UnidadOrganizativaId { get; private set; }

    public UnidadOrganizativa UnidadOrganizativa { get; private set; } = null!;

    public Guid CargoId { get; private set; }

    public Cargo Cargo { get; private set; } = null!;

    public Guid? PuestoSuperiorId { get; private set; }

    public Puesto? PuestoSuperior { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Descripcion { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Puesto> PuestosSubordinados => _puestosSubordinados;

    public IReadOnlyCollection<Ocupacion> Ocupaciones => _ocupaciones;

    public IReadOnlyCollection<Vacante> Vacantes => _vacantes;

    public void CambiarDatos(string codigo, string nombre, string? descripcion = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }

    public void CambiarPuestoSuperior(Guid? puestoSuperiorId)
    {
        if (puestoSuperiorId == Id)
        {
            throw new InvalidOperationException("Un puesto no puede ser superior de sí mismo.");
        }

        PuestoSuperiorId = puestoSuperiorId;
    }

    /// <summary>
    /// Actualiza los campos editables del puesto. NO modifica <see cref="Codigo"/>.
    /// </summary>
    public void Actualizar(string nombre, string? descripcion = null, Guid? puestoSuperiorId = null)
    {
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
        CambiarPuestoSuperior(puestoSuperiorId);
    }

    /// <summary>
    /// Desactiva el puesto (baja lógica).
    /// </summary>
    public void Desactivar()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reactiva el puesto. La verificación de unicidad de Codigo activo
    /// es responsabilidad del servicio de aplicación.
    /// </summary>
    public void Activar()
    {
        IsActive = true;
    }

    /// <summary>
    /// Factory de hidratación desde la capa de persistencia. Replica las
    /// invariantes de shape del constructor primario vía
    /// <see cref="ValidacionesDominio"/> y reusa <see cref="CambiarPuestoSuperior"/>
    /// para validar la invariante <c>puestoSuperiorId != Id</c>.
    /// </summary>
    internal static Puesto Reconstitute(
        Guid id,
        Guid unidadOrganizativaId,
        Guid cargoId,
        Guid? puestoSuperiorId,
        string codigo,
        string nombre,
        string? descripcion,
        bool isActive,
        UnidadOrganizativa? unidadOrganizativa,
        Cargo? cargo,
        DateTime createdAt,
        string? createdByUserId,
        DateTime? updatedAt,
        string? updatedByUserId,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedByUserId)
    {
        if (unidadOrganizativaId == Guid.Empty)
            throw new ArgumentException("La unidad organizativa es obligatoria.", nameof(UnidadOrganizativaId));
        if (cargoId == Guid.Empty)
            throw new ArgumentException("El cargo es obligatorio.", nameof(CargoId));

        var self = new Puesto
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

        self.UnidadOrganizativaId = unidadOrganizativaId;
        self.CargoId = cargoId;
        self.Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        self.Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        self.Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
        // CambiarPuestoSuperior valida la invariante Id != puestoSuperiorId
        // y setea PuestoSuperiorId. La lanzamos contra la instancia recién
        // creada, no contra el ctor primario, para evitar duplicar asignaciones.
        self.CambiarPuestoSuperior(puestoSuperiorId);
        self.IsActive = isActive;
        self.UnidadOrganizativa = unidadOrganizativa!;
        self.Cargo = cargo!;

        return self;
    }
}
