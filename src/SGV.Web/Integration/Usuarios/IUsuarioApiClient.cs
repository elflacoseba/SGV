using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Cliente HTTP tipado del módulo web de Usuarios.
/// Permite listar cuentas activas (catálogo), consultar paginado y
/// segmentado, obtener por id, ejecutar baja lógica, crear cuentas nuevas,
/// actualizar credenciales y roles de cuentas existentes, reactivarlas y
/// consultar el catálogo de roles asignado a un usuario.
/// </summary>
/// <remarks>
/// <para>
/// PR 2 del change <c>Implementa módulo usuarios</c>. La rama no exitosa
/// delega en los mappers comunes del shell web
/// (<c>ApiProblemReader</c>, <c>CommandResultMapper</c>) para preservar la
/// matriz <see cref="SGV.Contracts.Comun.ErrorCategoria"/>. Los enums
/// legacy <see cref="UsuarioErrorType"/> se siguen alimentando desde el
/// helper de mapeo interno (<c>MapCategoriaToLegacyType</c>) para
/// preservar source-compat con cualquier call site vigente.
/// </para>
/// <para>
/// El shape wire cumple el contrato <c>SGV.Contracts.Seguridad.Usuarios</c>
/// y los códigos de dominio <c>AutoBaja</c>, <c>PersonaInactiva</c>,
/// <c>UserNameDuplicado</c>, <c>EmailDuplicado</c>, <c>PersonaRequerida</c>
/// y <c>RolNoSoportado</c> llegan del backend en <c>ProblemDetails.Title</c>
/// y se exponen vía <see cref="UsuarioError.Code"/>.
/// </para>
/// </remarks>
public interface IUsuarioApiClient
{
    /// <summary>
    /// Catálogo plano de usuarios activos. Conservado como atajo para el
    /// dropdown que consume <c>GET /api/v1/usuarios</c> sin paginar (uso
    /// futuro en PR 4/4 si AdminSelect requiere un selector simple de
    /// usuarios).
    /// </summary>
    Task<IReadOnlyList<UsuarioDto>> GetAllActivasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la consulta paginada y segmentada vía
    /// <c>GET /api/v1/usuarios/consulta</c>. <c>query.Segmento</c> se
    /// serializa como query string <c>status=eliminadas</c> cuando
    /// corresponde; cualquier otro valor (incluyendo
    /// <see cref="UsuarioSegmentoListado.Activas"/>) omite el parámetro
    /// y deja que la API caiga a <c>activas</c> por defecto.
    /// </summary>
    /// <remarks>
    /// PR2-HALL: el shape wire del PR1 entrega <c>UsuarioListadoDto</c>
    /// como wrapper sobre <c>PagedResult&lt;UsuarioDto&gt;</c>
    /// (no como record plano con <c>Items/TotalCount/Page/PageSize</c>).
    /// El cliente preserva este wrapper para no tocar el contrato;
    /// las Pages consumidoras leerán <c>Result.Items</c> /
    /// <c>Result.TotalCount</c>. Quedó registrado en
    /// <c>apply-progress.md</c> §Desviaciones como brecha a cerrar.
    /// </remarks>
    Task<UsuarioListadoDto> QueryAsync(UsuarioListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un usuario activo por su identificador o <c>null</c> si no
    /// existe o ya no está disponible. <c>404</c> se traduce a
    /// <c>null</c> sin propagar excepción (equivale al patrón recuperable
    /// de detalles).
    /// </summary>
    Task<UsuarioDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuevo usuario vía <c>POST /api/v1/usuarios</c>. Devuelve
    /// éxito con el DTO persistido o un fallo tipado
    /// (<see cref="UsuarioErrorType.Validation"/> con <c>FieldErrors</c>,
    /// <see cref="UsuarioErrorType.Conflict"/> por
    /// <c>UserNameDuplicado</c>/<c>EmailDuplicado</c>/<c>PersonaYaTieneUsuario</c>,
    /// etc.).
    /// </summary>
    Task<UsuarioCommandResult> CreateAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza UserName/Email/Roles en una sola operación atómica vía
    /// <c>PUT /api/v1/usuarios/{id}</c>. Devuelve éxito con el DTO
    /// refrescado o un fallo tipado con la misma forma que
    /// <see cref="CreateAsync"/>.
    /// </summary>
    Task<UsuarioCommandResult> UpdateAsync(string id, ActualizarUsuarioRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta la baja lógica de un usuario vía
    /// <c>DELETE /api/v1/usuarios/{id}</c> y traduce la respuesta a un
    /// <see cref="UsuarioCommandResult"/>. <c>AutoBaja</c> se traduce a
    /// <see cref="SGV.Contracts.Comun.ErrorCategoria.Forbidden"/> con
    /// código <c>AutoBaja</c>; cualquier otra respuesta fallida se
    /// traduce por el mapper común.
    /// </summary>
    Task<UsuarioCommandResult> DesactivarAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Alias semánticamente equivalente a <see cref="DesactivarAsync"/>.
    /// Se conserva para alinear la nomenclatura del helper con el resto
    /// de los módulos web que exponen <c>DeleteAsync</c>; lo
    /// renombramos en este módulo a <c>Desactivar</c> porque el backend
    /// implementa soft-delete. Reservamos <c>Delete*</c> para el futuro
    /// borrado físico si llegara a existir.
    /// </summary>
    Task<UsuarioCommandResult> DeleteAsync(string id, CancellationToken cancellationToken = default)
        => DesactivarAsync(id, cancellationToken);

    /// <summary>
    /// Reactiva un usuario eliminado lógicamente vía
    /// <c>PATCH /api/v1/usuarios/{id}/reactivar</c> y traduce la
    /// respuesta a un <see cref="UsuarioCommandResult"/>. La regla D-02
    /// del design (Persona inactiva → Conflict) se traduce a
    /// <see cref="SGV.Contracts.Comun.ErrorCategoria.Conflict"/> con
    /// código <c>PersonaInactiva</c>.
    /// </summary>
    Task<UsuarioCommandResult> ReactivarAsync(string id, CancellationToken cancellationToken = default);
}
