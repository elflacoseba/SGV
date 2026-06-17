using SGV.Dominio.Comun;

namespace SGV.Dominio.Organizacion;

public sealed class UnidadOrganizativa : EntidadAuditable
{
    private readonly List<UnidadOrganizativa> _unidadesHijas = [];
    private readonly List<Puesto> _puestos = [];

    private UnidadOrganizativa()
    {
    }

    public UnidadOrganizativa(string codigo, string nombre, Guid tipoUnidadOrganizativaId, Guid? unidadPadreId = null)
    {
        CambiarDatos(codigo, nombre, tipoUnidadOrganizativaId);
        CambiarUnidadPadre(unidadPadreId);
        IsActive = true;
    }

    public Guid? UnidadPadreId { get; private set; }

    public UnidadOrganizativa? UnidadPadre { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public Guid TipoUnidadOrganizativaId { get; private set; }

    public TipoUnidadOrganizativa? TipoUnidadOrganizativa { get; private set; }

    public string? Descripcion { get; private set; }

    public DateOnly? VigenteDesde { get; private set; }

    public DateOnly? VigenteHasta { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<UnidadOrganizativa> UnidadesHijas => _unidadesHijas;

    public IReadOnlyCollection<Puesto> Puestos => _puestos;

    public void CambiarDatos(string codigo, string nombre, Guid tipoUnidadOrganizativaId, string? descripcion = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        if (tipoUnidadOrganizativaId == Guid.Empty)
        {
            throw new ArgumentException("El tipo de unidad organizativa es obligatorio.", nameof(TipoUnidadOrganizativaId));
        }
        TipoUnidadOrganizativaId = tipoUnidadOrganizativaId;
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }

    public void CambiarUnidadPadre(Guid? unidadPadreId)
    {
        if (unidadPadreId == Id)
        {
            throw new InvalidOperationException("Una unidad organizativa no puede ser padre de sí misma.");
        }

        UnidadPadreId = unidadPadreId;
    }

    public void DefinirVigencia(DateOnly? desde, DateOnly? hasta)
    {
        if (desde.HasValue && hasta.HasValue && hasta.Value < desde.Value)
        {
            throw new InvalidOperationException("La fecha de fin de vigencia no puede ser anterior al inicio.");
        }

        VigenteDesde = desde;
        VigenteHasta = hasta;
    }

    public void Desactivar() => IsActive = false;

    public void Activar() => IsActive = true;
}
