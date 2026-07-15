using System.Net;
using System.Text;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Common;

namespace SGV.Web.Integration.Personas;

/// <summary>
/// Cliente HTTP que consume los endpoints de personas de la API.
/// </summary>
/// <remarks>
/// PR #2 del change <c>2026-07-14-frontend-crud-personas</c>. La rama no
/// exitosa delega en <see cref="CommandResultMapper.Map"/> y
/// <see cref="ApiProblemReader.ReadAsync"/>, únicas fuentes de verdad
/// para la taxonomía <see cref="ErrorCategoria"/> en el shell web. Los
/// enums legacy <see cref="PersonaErrorType"/> se siguen alimentando
/// desde <see cref="MapCategoriaToLegacyType"/> para preservar
/// source-compat con cualquier call site vigente.
/// </remarks>
public sealed class PersonaApiClient(HttpClient httpClient) : IPersonaApiClient
{
    private const string BaseRoute = "/api/v1/personas";

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonaDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<PersonaDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<PersonaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<PersonaDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PersonaDeleteResult> DesactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", cancellationToken).ConfigureAwait(false);
        var result = await DeleteResultMapper
            .BuildDeleteResultAsync(response, HttpStatusCode.NoContent, cancellationToken)
            .ConfigureAwait(false);

        return new PersonaDeleteResult(
            result.Succeeded,
            result.StatusCode,
            result.Code,
            result.Message,
            result.Categoria);
    }

    /// <inheritdoc />
    public async Task<PersonaCommandResult> CreateAsync(CrearPersonaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .PostAsJsonAsync(BaseRoute, request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<PersonaDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return PersonaCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PersonaCommandResult> UpdateAsync(Guid id, ActualizarPersonaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .PutAsJsonAsync($"{BaseRoute}/{id}", request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<PersonaDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return PersonaCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PersonaListadoDto> QueryAsync(PersonaListQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildQueryUri(
            query.Page,
            query.PageSize,
            query.Search,
            query.Sort,
            query.Segmento);
        var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PersonaListadoDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? new PersonaListadoDto([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<PersonaCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content
                .ReadFromJsonAsync<PersonaDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return PersonaCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Construye la URI absoluta del endpoint paginado de personas.
    /// Espejo del <c>BuildQueryUri</c> de <c>CargoApiClient</c>: serializa
    /// <c>page/pageSize</c> obligatorios y agrega <c>search</c>/<c>sort</c>
    /// sólo si vienen poblados. <paramref name="segmento"/> se mapea a
    /// <c>status=eliminadas</c> cuando corresponde; cualquier otro valor
    /// (incluyendo <see cref="PersonaSegmentoListado.Activas"/>) omite el
    /// parámetro y deja que la API caiga al default <c>activas</c>.
    /// </summary>
    private static string BuildQueryUri(
        int page,
        int pageSize,
        string? search,
        string? sort,
        PersonaSegmentoListado segmento)
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

        if (segmento == PersonaSegmentoListado.Eliminadas)
        {
            builder.Append("&status=eliminadas");
        }

        return builder.ToString();
    }

    private static async Task<PersonaCommandResult> ToCommandResultAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader
            .ReadAsync(response, cancellationToken)
            .ConfigureAwait(false);
        var (categoria, code, message, statusCode) = CommandResultMapper.Map(response, parsed);

        var legacyType = MapCategoriaToLegacyType(categoria);
        var error = new PersonaError(legacyType, code, message, statusCode, categoria);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return PersonaCommandResult.Failure(error, parsed.FieldErrors);
        }

        return PersonaCommandResult.Failure(error);
    }

    /// <summary>
    /// Mapea <see cref="ErrorCategoria"/> al <see cref="PersonaErrorType"/>
    /// legacy preservando source-compat: <c>NotFound/Conflict/Validation</c>
    /// son 1-a-1; el resto (<c>Unauthorized/Forbidden/Transport/Unexpected</c>)
    /// colapsa a <see cref="PersonaErrorType.Validation"/> porque el
    /// enum histórico no tiene variantes equivalentes y la página
    /// siempre degrada a feedback legible.
    /// </summary>
    private static PersonaErrorType MapCategoriaToLegacyType(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => PersonaErrorType.NotFound,
        ErrorCategoria.Conflict => PersonaErrorType.Conflict,
        ErrorCategoria.Validation => PersonaErrorType.Validation,
        ErrorCategoria.Unauthorized => PersonaErrorType.Validation,
        ErrorCategoria.Forbidden => PersonaErrorType.Validation,
        ErrorCategoria.Transport => PersonaErrorType.Validation,
        ErrorCategoria.Unexpected => PersonaErrorType.Validation
    };
}
