using System.Net;
using System.Text;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Usuarios;

/// <summary>
/// Cliente HTTP que consume los endpoints de usuarios de la API.
/// </summary>
/// <remarks>
/// <para>
/// PR 2 del change <c>Implementa módulo usuarios</c>. La rama no
/// exitosa delega en <see cref="CommandResultMapper.Map"/>, única
/// fuente de verdad de la taxonomía <see cref="ErrorCategoria"/> en el
/// shell web. Los enums legacy <see cref="UsuarioErrorType"/> se
/// siguen alimentando desde <see cref="MapCategoriaToLegacyType"/>
/// (preservando source-compat con cualquier call site vigente); las
/// categorías sin equivalente colapsan a
/// <see cref="UsuarioErrorType.Validation"/> como en el resto del shell.
/// </para>
/// <para>
/// Los códigos de dominio <c>AutoBaja</c>, <c>PersonaInactiva</c>,
/// <c>UserNameDuplicado</c>, <c>EmailDuplicado</c>,
/// <c>PersonaRequerida</c> y <c>RolNoSoportado</c> llegan vía
/// <c>ProblemDetails.Title</c> y se preservan en
/// <see cref="UsuarioError.Code"/> para que el PageModel pueda
/// discriminar banners accionables sin ramificar por el texto del
/// Detail.
/// </para>
/// </remarks>
public sealed class UsuarioApiClient(HttpClient httpClient) : IUsuarioApiClient
{
    private const string BaseRoute = "/api/v1/usuarios";

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsuarioDto>> GetAllActivasAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<UsuarioDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<UsuarioDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<UsuarioDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UsuarioCommandResult> CreateAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await httpClient
            .PostAsJsonAsync(BaseRoute, request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<UsuarioDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return UsuarioCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UsuarioCommandResult> UpdateAsync(string id, ActualizarUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(request);

        var response = await httpClient
            .PutAsJsonAsync($"{BaseRoute}/{id}", request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<UsuarioDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return UsuarioCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UsuarioCommandResult> DesactivarAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // Backend PR1 expone 200 OK con DTO activo en DELETE (no 204)
        // para soportar la rama AutoBaja en código que pueda inspeccionar
        // el body; la rama 200 → Success existe también en otros
        // clientes del shell (CargoApiClient acepta 200 y 204 vía
        // IsSuccessStatusCode).
        var response = await httpClient
            .DeleteAsync($"{BaseRoute}/{id}", cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<UsuarioDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return UsuarioCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UsuarioCommandResult> ReactivarAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var response = await httpClient
            .PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<UsuarioDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return UsuarioCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UsuarioListadoDto> QueryAsync(UsuarioListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestUri = BuildQueryUri(
            query.Page,
            query.PageSize,
            query.Search,
            query.Sort,
            query.Segmento);
        var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // El contrato wire del PR1 entrega `UsuarioListadoDto` con un
        // único miembro `Result : PagedResult<UsuarioDto>`. El shell
        // expone ese mismo wrapper hacia las Pages; si en el futuro se
        // quiere aplanar al shape `(Items, TotalCount, Page, PageSize)`
        // usado por Personas/Cargos, eso queda como gap a cerrar en un
        // change posterior (ver apply-progress.md §Desviaciones).
        return await response.Content
            .ReadFromJsonAsync<UsuarioListadoDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? new UsuarioListadoDto(new PagedResult<UsuarioDto>(
                Items: [],
                TotalCount: 0,
                Page: query.Page,
                PageSize: query.PageSize));
    }

    /// <summary>
    /// Construye la URI absoluta del endpoint paginado de usuarios.
    /// Espejo del <c>BuildQueryUri</c> de <c>PersonaApiClient</c>:
    /// serializa <c>page/pageSize</c> obligatorios y agrega
    /// <c>search</c>/<c>sort</c> sólo si vienen poblados.
    /// <paramref name="segmento"/> se mapea a <c>status=eliminadas</c>
    /// cuando corresponde; cualquier otro valor (incluyendo
    /// <see cref="UsuarioSegmentoListado.Activas"/>) omite el parámetro
    /// y deja que la API caiga al default <c>activas</c>.
    /// </summary>
    private static string BuildQueryUri(
        int page,
        int pageSize,
        string? search,
        string? sort,
        UsuarioSegmentoListado segmento)
    {
        var builder = new StringBuilder($"{BaseRoute}/consulta?page={page}&pageSize={pageSize}");

        if (!string.IsNullOrWhiteSpace(search))
        {
            builder.Append("&search=");
            builder.Append(Uri.EscapeDataString(search));
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            builder.Append("&sort=");
            builder.Append(Uri.EscapeDataString(sort));
        }

        if (segmento == UsuarioSegmentoListado.Eliminadas)
        {
            builder.Append("&status=eliminadas");
        }

        return builder.ToString();
    }

    private static async Task<UsuarioCommandResult> ToCommandResultAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader
            .ReadAsync(response, cancellationToken)
            .ConfigureAwait(false);
        var (categoria, code, message, statusCode) = CommandResultMapper.Map(response, parsed);

        var legacyType = MapCategoriaToLegacyType(categoria);
        var error = new UsuarioError(legacyType, code, message, statusCode, categoria);

        // PR2-HALL-1 (mini-PR correctivo): cuando el backend
        // responde con `ValidationProblemDetails` y el
        // `ApiProblemReader` materializa el diccionario `errors`
        // por campo, lo propagamos vía el factory
        // `Failure(error, fieldErrors)`. La Razor Page de Create/Edit
        // (PR 4) lo aplica al ModelState bajo `Input.<clave>` para
        // que las tag helpers `asp-validation-for` rendereen el
        // mensaje junto al campo correspondiente. Mismo trato que
        // `CargoApiClient.ToCommandResultAsync` / `CargoApiClient.ToSkillCommandResultAsync`.
        if (parsed.FieldErrors is { Count: > 0 })
        {
            return UsuarioCommandResult.Failure(error, parsed.FieldErrors);
        }

        return UsuarioCommandResult.Failure(error);
    }

    /// <summary>
    /// Mapea <see cref="ErrorCategoria"/> al <see cref="UsuarioErrorType"/>
    /// legacy preservando source-compat: <c>NotFound/Conflict/Validation/Unauthorized</c>
    /// son 1-a-1; el resto (<c>Forbidden/Transport/Unexpected</c>)
    /// colapsa a <see cref="UsuarioErrorType.Validation"/> porque el
    /// enum histórico no tiene variantes equivalentes y la página
    /// siempre degrada a feedback legible.
    /// </summary>
    private static UsuarioErrorType MapCategoriaToLegacyType(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => UsuarioErrorType.NotFound,
        ErrorCategoria.Conflict => UsuarioErrorType.Conflict,
        ErrorCategoria.Validation => UsuarioErrorType.Validation,
        ErrorCategoria.Unauthorized => UsuarioErrorType.Unauthorized,
        ErrorCategoria.Forbidden => UsuarioErrorType.Validation,
        ErrorCategoria.Transport => UsuarioErrorType.Validation,
        ErrorCategoria.Unexpected => UsuarioErrorType.Validation
    };
}
