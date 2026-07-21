using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Integration.Personas;

/// <summary>
/// Cobertura mínima del cliente HTTP tipado <see cref="PersonaApiClient"/>
/// suficiente para PR #2 (Integration + DI). Las pruebas exhaustivas de
/// contrato (<c>IPersonaApiClientContractTests</c>) y la cobertura de las
/// Razor Pages (<c>IndexPageTests</c>, <c>CreatePageTests</c>, etc.) entran
/// en PR #4 según el design §Testing Strategy. Aquí sólo se verifica la
/// ruta feliz y la traducción status→categoria para los caminos que el
/// flujo actual de Pages/Personas/ (PR #3) termina invocando.
/// </summary>
public class PersonaApiClientQueryTests
{
    [Fact]
    public async Task QueryAsync_WithStatusEliminadas_SerializesStatusInUri()
    {
        var persona = new PersonaDto(Guid.NewGuid(), "L-DEL", "Ana", "García", null, null, null, null, null, null, IsActive: false);
        var payload = new PersonaListadoDto(
            Items: [persona], TotalCount: 1, Page: 1, PageSize: 10);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var client = new PersonaApiClient(httpClient);
        var result = await client.QueryAsync(
            new PersonaListQuery(Page: 1, PageSize: 10, Search: null, Sort: null,
                Segmento: PersonaSegmentoListado.Eliminadas));

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/personas/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("status=eliminadas",
            handler.LastRequest!.RequestUri!.Query,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithActivasSegment_DoesNotIncludeStatusParameter()
    {
        // REQ: persona-eliminadas nunca debe mezclarse en la respuesta de
        // activas. El cliente debe omitir el parámetro status cuando el
        // segmento es Activas (default del API).
        var payload = new PersonaListadoDto([], 0, 1, 10);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var client = new PersonaApiClient(httpClient);
        await client.QueryAsync(new PersonaListQuery(1, 10, null, null));

        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.DoesNotContain("status=", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithSortAndSearch_EscapesAndSerializesBoth()
    {
        // Triangulación: el orden y la búsqueda deben viajar en query string
        // para que el backend los aplique ANTES del Skip/Take (REQ-CM-01 del
        // design). Si el encoding falla, los acentos del search se rompen
        // y el orden se pierde silenciosamente entre páginas.
        var payload = new PersonaListadoDto([], 0, 1, 10);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var client = new PersonaApiClient(httpClient);
        await client.QueryAsync(new PersonaListQuery(1, 10, "garcía", "apellidos_asc"));

        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.Contains("sort=apellidos_asc", query, StringComparison.OrdinalIgnoreCase);
        // Uri.EscapeDataString escapa "í" como "%C3%AD" (UTF-8) — verificamos
        // que NO venga raw (lo que rompería parsing).
        Assert.Contains("search=garc%C3%ADa", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReactivateAsync_Http200_ReturnsSuccessDto()
    {
        var id = Guid.NewGuid();
        var dto = new PersonaDto(id, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var client = new PersonaApiClient(httpClient);
        var result = await client.ReactivarAsync(id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/personas/{id}/reactivar",
            handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ReactivarAsync_Http409_ReturnsFailureWithCategoriaConflict()
    {
        // Camino esperado durante la fase Eliminadas (PR #3): si el backend
        // rechaza la reactivación, el mapper debe devolver PersonaErrorType.Conflict
        // y ErrorCategoria.Conflict para que la Razor Page renderice el banner
        // accionable.
        var id = Guid.NewGuid();
        var problem = new
        {
            status = 409,
            title = "LegajoDuplicado",
            detail = "Ya existe una persona activa con el legajo L-001."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var client = new PersonaApiClient(httpClient);
        var result = await client.ReactivarAsync(id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PersonaErrorType.Conflict, result.Error!.Type);
        Assert.Equal("LegajoDuplicado", result.Error.Code);
        Assert.Equal(SGV.Contracts.Comun.ErrorCategoria.Conflict, result.Error.Categoria);
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// Stub HTTP handler que captura el último request y delega la
    /// respuesta a una factory configurable. Réplica del patrón que usan
    /// los tests del módulo Cargos; acá no agregamos dependencia al
    /// namespace Web/_Shared para mantener este archivo en su propio
    /// árbol y minimizar el blast radius del PR #2.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;
        public HttpRequestMessage? LastRequest { get; private set; }

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_factory(request));
        }
    }
}
