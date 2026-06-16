using SGV.Dominio.Comun;

namespace SGV.Dominio.Organizacion;

public sealed class TipoUnidadOrganizativa : EntidadBase
{
    private TipoUnidadOrganizativa()
    {
    }

    public TipoUnidadOrganizativa(string codigo, string nombre)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 100);
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
}
