using System.Net;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Setup;

namespace SGV.Web.Integration.Setup;

/// <summary>
/// Typed client HTTP para los endpoints one-time del setup inicial del
/// primer Administrador (issue #195). El shell web lo usa desde
/// <c>SignInModel.OnGetAsync</c> (para redirigir a <c>/auth/setup</c>
/// cuando <c>AspNetUsers</c> está vacía) y desde
/// <c>SetupModel.OnGetAsync</c>/<c>OnPostAsync</c> (para renderizar el
/// formulario y crear el admin).
/// </summary>
/// <remarks>
/// El cliente es explícitamente anónimo: NO usa
/// <see cref="Auth.ApiBearerTokenHandler"/> porque el endpoint
/// <c>POST /api/v1/setup</c> está decorado con
/// <c>[AllowAnonymous]</c> (chicken-and-egg: el primer admin no puede
/// autenticarse si todavía no existe). El status también es anónimo
/// porque se consulta desde <c>SignIn</c> antes de cualquier
/// autenticación.
/// </remarks>
public interface ISetupApiClient
{
    /// <summary>
    /// Consulta <c>GET /api/v1/setup/status</c> y devuelve el flag
    /// <see cref="SetupStatusResponse.RequiresSetup"/>. La
    /// implementación cachea el resultado con TTL 30s (design §2.3) y
    /// aplica fail-open: si la API está caída, devuelve
    /// <see cref="SetupStatusResponse"/> con
    /// <c>RequiresSetup=false</c> para no romper el acceso al sistema
    /// completo.
    /// </summary>
    Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el catálogo inmutable de <c>TipoDocumento</c> vía
    /// <c>GET /api/v1/tipos-documento</c> (que admite
    /// <c>[AllowAnonymous]</c> desde issue #195). Una respuesta no
    /// exitosa se traduce en una excepción
    /// <see cref="HttpRequestException"/> para que la página muestre
    /// el banner recuperable.
    /// </summary>
    Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía <c>POST /api/v1/setup</c> con el payload del primer
    /// Administrador. Devuelve un <see cref="SetupHttpResult"/> con
    /// el resultado tipado: éxito con <see cref="SetupResult"/>, o
    /// fallo con código de dominio <see cref="SetupErrorCode"/> y
    /// <c>FieldErrors</c> opcional para que la Razor Page los muestre
    /// junto al campo correspondiente.
    /// </summary>
    Task<SetupHttpResult> CrearAsync(SetupRequest request, CancellationToken cancellationToken = default);
}
