using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Cliente HTTP tipado del módulo web de Usuarios.
/// Permite listar cuentas activas (catálogo), consultar paginado y
/// segmentado, obtener por id, ejecutar el ciclo de lockout admin
/// (bloquear / desbloquear / eliminar físico), crear cuentas nuevas,
/// actualizar credenciales y roles de cuentas existentes y consultar el
/// catálogo de roles asignado a un usuario.
/// </summary>
/// <remarks>
/// <para>
/// PR 2 del change <c>Implementa módulo usuarios</c>. PR 3 del change
/// <c>2026-07-15-quita-soft-delete-usuario</c> reemplaza las operaciones
/// de baja lógica (<c>Desactivar</c>, <c>Reactivar</c>) por el ciclo de
/// lockout nativo de Identity (<c>Bloquear</c>, <c>Desbloquear</c>) y el
/// borrado físico (<c>Eliminar</c>); la rama no exitosa delega en los
/// mappers comunes del shell web (<c>ApiProblemReader</c>,
/// <c>CommandResultMapper</c>) para preservar la matriz
/// <see cref="SGV.Contracts.Comun.ErrorCategoria"/>. Los enums legacy
/// <see cref="UsuarioErrorType"/> se siguen alimentando desde el helper
/// de mapeo interno (<c>MapCategoriaToLegacyType</c>) para preservar
/// source-compat con cualquier call site vigente.
/// </para>
/// <para>
/// El shape wire cumple el contrato <c>SGV.Contracts.Seguridad.Usuarios</c>
/// y los códigos de dominio <c>AutoBloqueo</c>, <c>AutoEliminacion</c>,
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
    /// serializa como query string <c>status=bloqueadas</c> cuando
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
    /// Ejecuta el borrado físico de un usuario vía
    /// <c>DELETE /api/v1/usuarios/{id}</c> y traduce la respuesta a un
    /// <see cref="UsuarioCommandResult"/>. <c>AutoEliminacion</c> se
    /// traduce a <see cref="SGV.Contracts.Comun.ErrorCategoria.Forbidden"/>
    /// con código <c>AutoEliminacion</c>; cualquier otra respuesta
    /// fallida se traduce por el mapper común. El backend responde
    /// <c>204 No Content</c> en éxito; el cliente tipado trata el
    /// <c>204</c> como <see cref="UsuarioCommandResult.Success"/> con
    /// <c>Value</c> nulo (no se necesita el DTO post-borrado).
    /// </summary>
    Task<UsuarioCommandResult> EliminarAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Alias semánticamente equivalente a <see cref="EliminarAsync"/>.
    /// Se conserva como default interface method para no romper
    /// call sites históricos del shell (<c>cargo</c>,
    /// <c>habilidad</c>); el contrato Web canónico es
    /// <see cref="EliminarAsync"/>.
    /// </summary>
    Task<UsuarioCommandResult> DeleteAsync(string id, CancellationToken cancellationToken = default)
        => EliminarAsync(id, cancellationToken);

    /// <summary>
    /// Aplica el lockout administrativo de una cuenta vía
    /// <c>POST /api/v1/usuarios/{id}/bloquear</c>. El backend devuelve
    /// <c>200 OK</c> con el <see cref="UsuarioDto"/> actualizado
    /// (incluye <c>Bloqueado = true</c>) para que la Razor Page pueda
    /// confirmar el nuevo estado. <c>AutoBloqueo</c> se traduce a
    /// <see cref="SGV.Contracts.Comun.ErrorCategoria.Forbidden"/> con
    /// código <c>AutoBloqueo</c>.
    /// </summary>
    Task<UsuarioCommandResult> BloquearAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quita el lockout administrativo de una cuenta vía
    /// <c>POST /api/v1/usuarios/{id}/desbloquear</c>. El backend devuelve
    /// <c>200 OK</c> con el <see cref="UsuarioDto"/> actualizado
    /// (incluye <c>Bloqueado = false</c>).
    /// </summary>
    Task<UsuarioCommandResult> DesbloquearAsync(string id, CancellationToken cancellationToken = default);
}
