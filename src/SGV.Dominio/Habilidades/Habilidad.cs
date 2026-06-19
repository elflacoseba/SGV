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

    /// <summary>
    /// Código único de la habilidad. Se define en la creación y NO puede modificarse.
    /// </summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Descripcion { get; private set; }

    public string? Categoria { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Reemplaza todos los campos editables y el código. Reservado al constructor
    /// y al mapper de persistencia (slice 2).
    /// </summary>
    public void CambiarDatos(string codigo, string nombre, string? categoria = null, string? descripcion = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        Categoria = ValidacionesDominio.Opcional(categoria, nameof(Categoria), 100);
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }

    /// <summary>
    /// Actualiza los campos editables de la habilidad. NO modifica <see cref="Codigo"/>.
    /// La verificación de unicidad activa del código es responsabilidad del servicio de aplicación.
    /// </summary>
    public void Actualizar(string nombre, string? categoria = null, string? descripcion = null)
    {
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        Categoria = ValidacionesDominio.Opcional(categoria, nameof(Categoria), 100);
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }

    /// <summary>
    /// Desactiva la habilidad (baja lógica). No elimina el registro y no
    /// altera asignaciones existentes a cargos o personas.
    /// </summary>
    public void Desactivar()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reactiva la habilidad. La verificación de unicidad activa de Codigo
    /// es responsabilidad del servicio de aplicación.
    /// </summary>
    public void Activar()
    {
        IsActive = true;
    }
}
