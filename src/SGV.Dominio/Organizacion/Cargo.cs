using SGV.Dominio.Comun;
using SGV.Dominio.Habilidades;

namespace SGV.Dominio.Organizacion;

public sealed class Cargo : EntidadAuditable
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
    /// Desactiva el cargo. Si la colección de Puestos está cargada y contiene
    /// al menos un Puesto activo, lanza <see cref="InvalidOperationException"/>.
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
}
