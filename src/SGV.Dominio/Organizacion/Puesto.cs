using SGV.Dominio.Comun;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Vacantes;

namespace SGV.Dominio.Organizacion;

public sealed class Puesto : EntidadAuditable
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
}
