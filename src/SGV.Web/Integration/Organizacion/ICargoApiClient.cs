using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP tipado del módulo web de Cargos.
/// Permite listar activos, obtener por id y ejecutar baja lógica.
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
}