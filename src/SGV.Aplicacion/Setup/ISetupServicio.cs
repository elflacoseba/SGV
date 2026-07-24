using SGV.Contracts.Setup;

namespace SGV.Aplicacion.Setup;

/// <summary>
/// Puerto de aplicación para el setup one-time del primer Administrador
/// (issue #195). Diseñado para resolver el chicken-and-egg entre la
/// ausencia de usuarios y la imposibilidad de ejecutar los flujos
/// <c>POST /api/v1/personas</c> + <c>POST /api/v1/usuarios</c> que
/// requieren <c>[Authorize(Roles = RolesSgv.Administrador)]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No reusa <c>PersonaServicioComandos</c> + <c>UsuarioServicioComandos</c></b>
/// porque ambos esperan un <c>usuarioActual.UserId</c> para auditoría y
/// abren transacciones separadas; setup necesita una transacción única
/// que abarque Persona + Usuario + auditoría con
/// <c>usuarioOperadorId="system"</c>.
/// </para>
/// <para>
/// Implementado en <c>SGV.Infraestructura.Setup.SetupServicio</c>.
/// </para>
/// </remarks>
public interface ISetupServicio
{
    /// <summary>
    /// Indica si el sistema requiere el flujo de setup one-time.
    /// Se calcula con un <c>EXISTS(SELECT 1 FROM AspNetUsers)</c>
    /// (O(1) por PK clustered sobre <c>Id</c>).
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <c>SetupStatusResponse(RequiresSetup: true)</c> cuando
    /// <c>AspNetUsers</c> está vacía; <c>false</c> en caso contrario.
    /// </returns>
    Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken ct = default);

    /// <summary>
    /// Crea atómicamente la primera Persona + Usuario Identity + rol
    /// <c>Administrador</c> cuando <c>AspNetUsers</c> está vacía.
    /// </summary>
    /// <param name="request">Datos del formulario web de setup.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <see cref="SetupCommandResult"/> con <c>IsSuccess=true</c> y
    /// <see cref="Contracts.Setup.SetupResult"/> poblado, o
    /// <c>IsSuccess=false</c> con <see cref="SetupError"/> que mapea
    /// a un HTTP 400 / 409 / 500 vía <see cref="SetupError.StatusCode"/>.
    /// </returns>
    Task<SetupCommandResult> CrearAdminAsync(SetupRequest request, CancellationToken ct = default);
}
