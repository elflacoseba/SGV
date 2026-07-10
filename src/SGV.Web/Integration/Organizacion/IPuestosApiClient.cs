using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP tipado del módulo web de Puestos.
/// Permite listar activos, obtener por id, ejecutar baja lógica, crear,
/// editar y reactivar puestos.
/// </summary>
public interface IPuestosApiClient
{
    /// <summary>Lista todos los puestos activos.</summary>
    Task<IReadOnlyList<PuestoDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene un puesto activo por id o <c>null</c> si no existe.</summary>
    Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un puesto. Devuelve éxito con DTO o fallo tipado
    /// (<see cref="PuestoErrorType.Validation"/> con <c>FieldErrors</c>,
    /// <see cref="PuestoErrorType.Conflict"/> si el código está duplicado, etc.).
    /// </summary>
    Task<PuestoCommandResult> CreateAsync(CrearPuestoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza Nombre/Descripcion?/PuestoSuperiorId?. Mapea 400 (FieldErrors)
    /// y 409 (<c>CodigoDuplicado</c>, <c>PuestoSuperiorInvalido</c>).
    /// </summary>
    Task<PuestoCommandResult> UpdateAsync(Guid id, ActualizarPuestoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta baja lógica vía <c>DELETE /api/v1/puestos/{id}</c>. Traduce
    /// 204 → Succeeded y 404/409 → Failure.
    /// </summary>
    Task<PuestoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactiva un puesto vía <c>PATCH /api/v1/puestos/{id}/reactivar</c>.
    /// Mapea 409 por código duplicado.
    /// </summary>
    Task<PuestoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
