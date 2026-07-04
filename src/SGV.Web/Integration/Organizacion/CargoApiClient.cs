using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Cliente HTTP que consume los endpoints de cargos de la API.
/// </summary>
public sealed class CargoApiClient(HttpClient httpClient) : ICargoApiClient
{
    private const string BaseRoute = "/api/v1/cargos";
    private const string NivelesRoute = "/api/v1/niveles-cargo";

    /// <inheritdoc />
    public async Task<IReadOnlyList<CargoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BaseRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<CargoDto>>(cancellationToken: cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CargoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new CargoDeleteResult(true, response.StatusCode, null, null);
        }

        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
        }
        catch (NotSupportedException)
        {
        }
        catch (HttpRequestException)
        {
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return new CargoDeleteResult(
            false,
            response.StatusCode,
            problem?.Title,
            problem?.Detail);
    }

    /// <inheritdoc />
    public async Task<CargoCommandResult> CreateAsync(CrearCargoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken: cancellationToken);
            return CargoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CargoCommandResult> UpdateAsync(Guid id, ActualizarCargoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/{id}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken: cancellationToken);
            return CargoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NivelCargoDto>> GetNivelesAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(NivelesRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<NivelCargoDto>>(cancellationToken: cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildQueryUri(query.Page, query.PageSize, query.Search, query.Sort, query.Status);
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<CargoDto>>(cancellationToken: cancellationToken)
            ?? new PagedResult<CargoDto>([], 0, query.Page, query.PageSize);
    }

    /// <inheritdoc />
    public async Task<CargoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsync($"{BaseRoute}/{id}/reactivar", null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<CargoDto>(cancellationToken: cancellationToken);
            return CargoCommandResult.Success(dto!);
        }

        return await ToCommandResultAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CargoSkillDetailDto>> GetSkillsAsync(Guid cargoId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BaseRoute}/{cargoId}/skills", cancellationToken);

        // 404 → estado vacío recuperable para la grilla editable de PR3b;
        // cualquier otro status que no sea 2xx sigue propagándose como
        // excepción para que la Razor Page muestre un error recuperable
        // (alineado con el patrón GetByIdAsync, que devuelve null en 404 y
        // deja pasar el resto a EnsureSuccessStatusCode).
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<CargoSkillDetailDto>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<CargoSkillCommandResult> UpsertSkillAsync(Guid cargoId, Guid skillId, AsignarCargoSkillRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .PutAsJsonAsync($"{BaseRoute}/{cargoId}/skills/{skillId}", request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            // PR3a review follow-up (R1): si el backend responde 2xx con body
            // vacío o con el literal JSON `null`, ReadFromJsonAsync o devuelve
            // null o tira JsonException. La rama `Success(dto!)` original
            // propagaba esa anomalía como un "éxito con DTO null" o como un
            // crash — ninguno de los dos es aceptable para PR3b, que necesita
            // distinguir "asignación persistida" de "asignación sin payload".
            // Capturamos ambos casos y devolvemos un Failure tipado
            // Validation/EmptyBody para que la Razor Page muestre el mensaje
            // estándar sin filtrar una excepción nativa al usuario.
            CargoSkillDto? dto;
            try
            {
                dto = await response.Content
                    .ReadFromJsonAsync<CargoSkillDto>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException)
            {
                dto = null;
            }

            if (dto is null)
            {
                return CargoSkillCommandResult.Failure(
                    new CargoSkillError(
                        CargoSkillErrorType.Validation,
                        "EmptyBody",
                        "El servidor respondió 200 sin payload."));
            }

            return CargoSkillCommandResult.Success(dto);
        }

        return await ToSkillCommandResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CargoSkillDeleteResult> DeleteSkillAsync(Guid cargoId, Guid skillId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient
            .DeleteAsync($"{BaseRoute}/{cargoId}/skills/{skillId}", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new CargoSkillDeleteResult(true, response.StatusCode, null, null);
        }

        // PR3a review follow-up (R3): el helper previo colapsaba 401/403/409/4xx
        // en un Failure genérico con Code=null/Message=null cuando el body no
        // traía ProblemDetails parseable. Bifurcamos por status, reutilizando
        // <see cref="MapSkillError"/> + <see cref="ReadSkillProblemAsync"/>
        // que comparten la lógica de parseo + try/catch con
        // <see cref="ToSkillCommandResultAsync"/>. Así la Razor Page de
        // PR3b puede decidir entre "redirigir a login" (401), "mostrar
        // Acceso denegado" (403), "mostrar conflicto" (409) o "Servicio no
        // disponible" (5xx) sin depender de StatusCode parsing manual.
        var (_, code, message) = MapSkillError(response.StatusCode);
        var parsed = await ReadSkillProblemAsync(response, (code, message), cancellationToken)
            .ConfigureAwait(false);

        return new CargoSkillDeleteResult(
            false,
            response.StatusCode,
            parsed.Code,
            parsed.Message);
    }

    /// <summary>
    /// Defaults tipados por status para la respuesta de error del subrecurso
    /// <c>PUT/DELETE /api/v1/cargos/{cargoId}/skills/{skillId}</c>. Cuando el
    /// backend no entrega un <see cref="ProblemDetails"/> parseable (e.g. un
    /// 401 con body vacío, un 5xx con HTML), usamos estos valores para
    /// poblar <c>Code</c>/<c>Message</c> del resultado tipado y, para el
    /// helper <see cref="ToSkillCommandResultAsync"/>, el
    /// <see cref="CargoSkillErrorType"/> que la UI necesita. El mapping
    /// refleja los códigos que efectivamente emite el controller de PR2
    /// (200/400/401/403/404 para PUT, 204/401/403/404 para DELETE) más un
    /// fallback 5xx preparado para evoluciones futuras del backend.
    /// </summary>
    private static (CargoSkillErrorType Type, string Code, string Message) MapSkillError(HttpStatusCode status) =>
        status switch
        {
            HttpStatusCode.BadRequest => (CargoSkillErrorType.Validation, "BadRequest", "Solicitud inválida."),
            HttpStatusCode.NotFound => (CargoSkillErrorType.NotFound, "NotFound", "Recurso no encontrado."),
            HttpStatusCode.Unauthorized => (CargoSkillErrorType.Unauthorized, "Unauthorized", "Acceso no autorizado."),
            HttpStatusCode.Forbidden => (CargoSkillErrorType.Forbidden, "Forbidden", "Acceso denegado."),
            HttpStatusCode.Conflict => (CargoSkillErrorType.Conflict, "Conflict", "Conflicto."),
            _ when (int)status >= 500 => (CargoSkillErrorType.Transport, "TransportError", "Servicio no disponible."),
            _ => (CargoSkillErrorType.Validation, "Unexpected", "Respuesta inesperada del servidor.")
        };

    /// <summary>
    /// Lee el body de una respuesta como <see cref="ProblemDetails"/>,
    /// absorbiendo <see cref="NotSupportedException"/>,
    /// <see cref="HttpRequestException"/> y
    /// <see cref="System.Text.Json.JsonException"/> para no propagar
    /// excepciones nativas al consumidor del cliente. Si el parseo
    /// devuelve <c>null</c> (body vacío o literal <c>null</c>), devuelve
    /// los <paramref name="defaults"/> provistos; si devuelve un
    /// <see cref="ProblemDetails"/> válido, devuelve <c>Title</c>/<c>Detail</c>
    /// cuando estén poblados y los <paramref name="defaults"/> cuando
    /// alguno esté vacío. Es la base compartida por
    /// <see cref="ToSkillCommandResultAsync"/> y
    /// <see cref="DeleteSkillAsync"/>.
    /// </summary>
    private static async Task<(string Code, string Message)> ReadSkillProblemAsync(
        HttpResponseMessage response,
        (string Code, string Message) defaults,
        CancellationToken cancellationToken)
    {
        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
        }
        catch (HttpRequestException)
        {
        }
        catch (System.Text.Json.JsonException)
        {
        }

        var code = string.IsNullOrEmpty(problem?.Title) ? defaults.Code : problem.Title;
        var message = string.IsNullOrEmpty(problem?.Detail) ? defaults.Message : problem.Detail;
        return (code, message);
    }

    private static string BuildQueryUri(int page, int pageSize, string? search, string? sort = null, string? status = null)
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

        if (!string.IsNullOrWhiteSpace(status))
        {
            builder.Append("&status=");
            builder.Append(Uri.EscapeDataString(status));
        }

        return builder.ToString();
    }

    private static async Task<CargoCommandResult> ToCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: cancellationToken);
            if (problem?.Errors is { Count: > 0 })
            {
                var fieldErrors = problem.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                return CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Validation, problem.Title ?? "ValidationError", problem.Detail ?? "Uno o más campos son inválidos."),
                    fieldErrors);
            }

            return CargoCommandResult.Failure(
                new CargoError(CargoErrorType.Validation, problem?.Title ?? "BadRequest", problem?.Detail ?? "Solicitud inválida."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
            return CargoCommandResult.Failure(
                new CargoError(CargoErrorType.NotFound, problem?.Title ?? "NotFound", problem?.Detail ?? "Recurso no encontrado."));
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
            return CargoCommandResult.Failure(
                new CargoError(CargoErrorType.Conflict, problem?.Title ?? "Conflict", problem?.Detail ?? "Conflicto."));
        }

        return CargoCommandResult.Failure(
            new CargoError(CargoErrorType.Validation, "Unexpected", "Respuesta inesperada del servidor."));
    }

    /// <summary>
    /// Traduce una respuesta no exitosa del subrecurso <c>PUT
    /// /api/v1/cargos/{cargoId}/skills/{skillId}</c> a un
    /// <see cref="CargoSkillCommandResult"/>. Para 400 bifurca entre
    /// <c>ValidationProblemDetails</c> (errores por campo) y
    /// <c>ProblemDetails</c> (fallo plano) porque sólo ese status puede
    /// traer <c>FieldErrors</c>. El resto de los códigos
    /// (404/401/403/409/5xx/fallback) pasan por
    /// <see cref="ReadSkillProblemAsync"/> + <see cref="MapSkillError"/>,
    /// helpers compartidos con <see cref="DeleteSkillAsync"/> (R2+R5 del
    /// review follow-up). Se mantiene deliberadamente separada de
    /// <see cref="ToCommandResultAsync"/> — el subrecurso sólo emite
    /// 200/400/401/403/404/409/5xx en la rama de errores; cada código se
    /// traduce a un <see cref="CargoSkillErrorType"/> específico para que
    /// la Razor Page de PR3b pueda distinguir entre validación,
    /// conflicto, falta de autenticación, falta de autorización y errores
    /// de servidor/transporte sin depender del texto del mensaje.
    /// </summary>
    private static async Task<CargoSkillCommandResult> ToSkillCommandResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var validation = await response.Content
                .ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (validation?.Errors is { Count: > 0 })
            {
                var fieldErrors = validation.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
                return CargoSkillCommandResult.Failure(
                    new CargoSkillError(
                        CargoSkillErrorType.Validation,
                        validation.Title ?? "DatosInvalidos",
                        validation.Detail ?? "Uno o más campos del vínculo contienen errores de validación."),
                    fieldErrors);
            }

            return CargoSkillCommandResult.Failure(
                new CargoSkillError(
                    CargoSkillErrorType.Validation,
                    validation?.Title ?? "BadRequest",
                    validation?.Detail ?? "Solicitud inválida."));
        }

        var defaults = MapSkillError(response.StatusCode);
        var (code, message) = await ReadSkillProblemAsync(
            response,
            (defaults.Code, defaults.Message),
            cancellationToken).ConfigureAwait(false);

        return CargoSkillCommandResult.Failure(
            new CargoSkillError(defaults.Type, code, message));
    }
}
