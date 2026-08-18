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
    private const string TiposDocumentoRoute = "/api/v1/tipos-documento";

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
            query.Segmento,
            query.SoloSinUsuario);
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonaDto>> BuscarAsync(
        string? search,
        int take = 50,
        bool? soloSinUsuario = null,
        CancellationToken cancellationToken = default)
    {
        // D-PE-03: server-side typeahead. Construye la query string con
        // los mismos flags que QueryAsync pero serializa `take` (cap
        // defensivo server-side: 100) y omite paginación.
        var builder = new StringBuilder($"{BaseRoute}/buscar?take={take}");

        if (!string.IsNullOrWhiteSpace(search))
        {
            builder.Append("&q=");
            builder.Append(Uri.EscapeDataString(search));
        }

        if (soloSinUsuario == true)
        {
            builder.Append("&soloSinUsuario=true");
        }

        var response = await httpClient
            .GetAsync(builder.ToString(), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<PersonaDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken cancellationToken = default)
    {
        // Issue #147 PR3: consumido por Create/Edit para popular el <select>.
        // Espejo de CargoApiClient.GetNivelesAsync (devuelve lista vacía si el
        // body viene vacío para preservar el contrato del fake/page model).
        var response = await httpClient
            .GetAsync(TiposDocumentoRoute, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<TipoDocumentoDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonaSkillDetailDto>> GetSkillsAsync(Guid personaId, CancellationToken cancellationToken = default)
    {
        // Subrecurso persona-skill (Slice 2 / REQ-WEB-04): mismo patrón que
        // CargoApiClient.GetSkillsAsync. 404 → estado vacío recuperable
        // para la grilla editable de Slice 3a; cualquier otro status que no
        // sea 2xx sigue propagándose como excepción para que la Razor Page
        // muestre un error recuperable.
        var response = await httpClient
            .GetAsync($"{BaseRoute}/{personaId}/skills", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<PersonaSkillDetailDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<PersonaSkillCommandResult> UpsertSkillAsync(Guid personaId, Guid skillId, AsignarPersonaSkillRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .PutAsJsonAsync($"{BaseRoute}/{personaId}/skills/{skillId}", request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            // Si el backend responde 2xx con body vacío,
            // ReadFromJsonAsync devuelve null o tira JsonException.
            // Capturamos ambos y devolvemos un Failure tipado
            // Validation/EmptyBody para que la Razor Page muestre el
            // mensaje estándar sin filtrar una excepción nativa al
            // usuario (espejo del patrón de CargoApiClient.UpsertSkillAsync).
            PersonaSkillDto? dto;
            try
            {
                dto = await response.Content
                    .ReadFromJsonAsync<PersonaSkillDto>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException)
            {
                dto = null;
            }

            if (dto is null)
            {
                return PersonaSkillCommandResult.Failure(
                    new PersonaSkillError(
                        PersonaSkillErrorType.Validation,
                        "EmptyBody",
                        "El servidor respondió 200 sin payload.",
                        Categoria: ErrorCategoria.Validation));
            }

            return PersonaSkillCommandResult.Success(dto);
        }

        return await ToSkillCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PersonaSkillDeleteResult> DeleteSkillAsync(Guid personaId, Guid skillId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .DeleteAsync($"{BaseRoute}/{personaId}/skills/{skillId}", cancellationToken)
            .ConfigureAwait(false);
        var result = await DeleteResultMapper
            .BuildDeleteResultAsync(response, HttpStatusCode.NoContent, cancellationToken)
            .ConfigureAwait(false);

        return new PersonaSkillDeleteResult(
            result.Succeeded,
            result.StatusCode,
            result.Code,
            result.Message,
            result.Categoria);
    }

    /// <summary>
    /// Construye la URI absoluta del endpoint paginado de personas.
    /// Espejo del <c>BuildQueryUri</c> de <c>CargoApiClient</c>: serializa
    /// <c>page/pageSize</c> obligatorios y agrega <c>search</c>/<c>sort</c>
    /// sólo si vienen poblados. <paramref name="segmento"/> se mapea a
    /// <c>status=eliminadas</c> cuando corresponde; cualquier otro valor
    /// (incluyendo <see cref="PersonaSegmentoListado.Activas"/>) omite el
    /// parámetro y deja que la API caiga al default <c>activas</c>.
    /// <paramref name="soloSinUsuario"/> se serializa como
    /// <c>&amp;soloSinUsuario=true</c> sólo cuando es <c>true</c>; los
    /// valores <c>null</c> o <c>false</c> se omiten para preservar
    /// back-compat URI con los consumidores vigentes que no envían el
    /// flag (Index Personas, typeahead). Cambio WU-4 del change
    /// <c>2026-07-17-buscador-personas-modal</c>.
    /// </summary>
    private static string BuildQueryUri(
        int page,
        int pageSize,
        string? search,
        string? sort,
        PersonaSegmentoListado segmento,
        bool? soloSinUsuario)
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

        if (soloSinUsuario == true)
        {
            builder.Append("&soloSinUsuario=true");
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

        // D-PE-02: mapeo 1-a-1 con ErrorCategoria vía mapper compartido
        // (single source of truth en ErrorCategoriaMappers). Elimina el
        // switch privado que colapsaba Unauthorized/Forbidden/Transport/
        // Unexpected → Validation y disparaba el warning CS8524 endémico.
        var legacyType = ErrorCategoriaMappers.ToTipoPersona(categoria);
        var error = new PersonaError(legacyType, code, message, statusCode, categoria);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return PersonaCommandResult.Failure(error, parsed.FieldErrors);
        }

        return PersonaCommandResult.Failure(error);
    }

    /// <summary>
    /// Construye un <see cref="PersonaSkillCommandResult"/> a partir de
    /// una respuesta HTTP no exitosa del subrecurso
    /// <c>PUT /api/v1/personas/{personaId}/skills/{skillId}</c>. Para
    /// <c>400</c> con <c>ValidationProblemDetails</c> conserva los
    /// <c>FieldErrors</c>; el resto pasa por el mapper común. La rama no
    /// exitosa del subrecurso delega en
    /// <see cref="CommandResultMapper.Map"/> (única fuente de verdad del
    /// shell web para la taxonomía <see cref="ErrorCategoria"/>) y
    /// preserva <see cref="PersonaSkillError.Categoria"/> poblado por el
    /// mapper para que el PageModel de Slice 3a pueda ramificar por la
    /// taxonomía común sin consultar el enum del subdominio.
    /// </summary>
    private static async Task<PersonaSkillCommandResult> ToSkillCommandResultAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var parsed = await ApiProblemReader
            .ReadAsync(response, cancellationToken)
            .ConfigureAwait(false);
        var (categoria, code, message, statusCode) = CommandResultMapper.Map(response, parsed);

        var legacyType = MapCategoriaToLegacySkillType(categoria);
        var error = new PersonaSkillError(legacyType, code, message, statusCode, categoria);

        if (parsed.FieldErrors is { Count: > 0 })
        {
            return PersonaSkillCommandResult.Failure(error, parsed.FieldErrors);
        }

        return PersonaSkillCommandResult.Failure(error);
    }

    /// <summary>
    /// Mapea <see cref="ErrorCategoria"/> al <see cref="PersonaSkillErrorType"/>
    /// legacy. <see cref="PersonaSkillErrorType"/> sólo cubre
    /// <c>NotFound</c> y <c>Validation</c>; las categorías que el
    /// subdominio no emite por contrato (<c>Conflict/Unauthorized/Forbidden/Transport/Unexpected</c>)
    /// colapsan a <see cref="PersonaSkillErrorType.Validation"/> vía el
    /// fallback de <see cref="ErrorCategoriaMappers.ToTipoPersonaSkill"/>
    /// (que lanza <see cref="NotSupportedException"/>). La taxonomía
    /// observable sigue siendo <see cref="ErrorCategoria"/> vía
    /// <see cref="PersonaSkillError.Categoria"/> (Slice 1 / decision
    /// #1284); el campo legacy <c>Type</c> se preserva por source-compat
    /// pero NO es la fuente de verdad.
    /// </summary>
    private static PersonaSkillErrorType MapCategoriaToLegacySkillType(ErrorCategoria categoria)
    {
        try
        {
            return ErrorCategoriaMappers.ToTipoPersonaSkill(categoria);
        }
        catch (NotSupportedException)
        {
            // PersonaSkillErrorType sólo cubre NotFound y Validation.
            // El resto colapsa a Validation para preservar un campo
            // Type no nulo y permitir feedback legible en la página.
            return PersonaSkillErrorType.Validation;
        }
    }
}
