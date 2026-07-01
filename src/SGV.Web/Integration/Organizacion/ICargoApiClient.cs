using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP tipado del módulo web de Cargos.
/// Permite listar activos, obtener por id, ejecutar baja lógica, crear
/// nuevos cargos y consultar el catálogo de niveles de cargo.
/// </summary>
public interface ICargoApiClient
{
    /// <summary>
    /// Lista todos los cargos activos.
    /// </summary>
    Task<IReadOnlyList<CargoDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un cargo activo por su identificador o <c>null</c> si no existe o ya no está disponible.
    /// </summary>
    Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la baja lógica de un cargo y traduce la respuesta a un <see cref="CargoDeleteResult"/>.
    /// </summary>
    Task<CargoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuevo cargo. Devuelve éxito con el DTO persistido o un fallo tipado
    /// (<see cref="CargoErrorType.Validation"/> con <c>FieldErrors</c>,
    /// <see cref="CargoErrorType.Conflict"/> si el código está duplicado contra un
    /// cargo activo, etc.).
    /// </summary>
    Task<CargoCommandResult> CreateAsync(CrearCargoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el catálogo de niveles de cargo disponible para asociar a un cargo.
    /// </summary>
    Task<IReadOnlyList<NivelCargoDto>> GetNivelesAsync(CancellationToken cancellationToken = default);
}
