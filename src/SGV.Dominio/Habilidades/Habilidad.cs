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
    /// Código único de la habilidad. Mutable solo desde dentro de la entidad
    /// vía <see cref="Actualizar"/>; la verificación de unicidad activa contra
    /// otras Habilidades es responsabilidad del servicio de aplicación.
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
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), HabilidadRules.CodigoMaxLength);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        Categoria = ValidacionesDominio.Opcional(categoria, nameof(Categoria), 100);
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }

    /// <summary>
    /// Actualiza los campos editables de la habilidad, incluido <see cref="Codigo"/>.
    /// La unicidad activa del código se valida en el servicio de aplicación
    /// antes de invocar este método; este solo aplica reglas de shape
    /// (requerido, longitud máxima). Delega en <see cref="CambiarDatos"/>
    /// para evitar duplicación de invariantes entre el constructor y la
    /// actualización.
    /// </summary>
    /// <param name="codigo">Nuevo código de la habilidad. Requerido, máximo <see cref="HabilidadRules.CodigoMaxLength"/> caracteres.</param>
    /// <param name="nombre">Nuevo nombre de la habilidad. Requerido, máximo 200 caracteres.</param>
    /// <param name="categoria">Categoría opcional, máximo 100 caracteres.</param>
    /// <param name="descripcion">Descripción opcional, máximo 1000 caracteres.</param>
    public void Actualizar(string codigo, string nombre, string? categoria = null, string? descripcion = null)
        => CambiarDatos(codigo, nombre, categoria, descripcion);

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
