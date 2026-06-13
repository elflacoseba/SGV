using SGV.Dominio.Comun;

namespace SGV.Dominio.Habilidades;

public sealed class Habilidad : EntidadAuditable
{
    private Habilidad()
    {
    }

    public Habilidad(string codigo, string nombre, string? categoria = null, string? descripcion = null)
    {
        CambiarDatos(codigo, nombre, categoria, descripcion);
        IsActive = true;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Descripcion { get; private set; }

    public string? Categoria { get; private set; }

    public bool IsActive { get; private set; }

    public void CambiarDatos(string codigo, string nombre, string? categoria = null, string? descripcion = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        Categoria = ValidacionesDominio.Opcional(categoria, nameof(Categoria), 100);
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }
}
