using System.Net;
using System.Text;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests RED→GREEN de los métodos de mutación (Crear/Actualizar/Finalizar/
/// Eliminar/Reactivar) del cliente HTTP tipado <see cref="OcupacionApiClient"/>
/// introducidos en Slice 3a del change #208. Espejo del patrón de
/// <c>PuestosApiClientTests</c>: ejercita <c>ToCommandResultAsync</c> vía
/// <see cref="RecordingHandler"/> con escenarios 2xx/4xx/5xx y assertea
/// mapeo a <see cref="OcupacionCommandResult"/>.
/// </summary>
public sealed class OcupacionApiClientMutationTests
{
    private const string BaseUrl = "https://api.test";
    private const string BaseRoute = "/api/v1/ocupaciones";

    private static OcupacionApiClient BuildClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(10)
        };
        return new OcupacionApiClient(http);
    }

    private static StringContent JsonContent(string body) =>
        new(body, Encoding.UTF8, "application/json");

    private static CrearOcupacionRequest SampleCrearRequest() => new(
        PersonaId: Guid.NewGuid(),
        PuestoId: Guid.NewGuid(),
        FechaInicio: new DateOnly(2026, 1, 15),
        TipoAsignacion: OcupacionTipoAsignacion.Permanente,
        Observaciones: null);

    private static string SuccessDtoJson(Guid id) => $$"""
    {
      "id":"{{id:D}}",
      "personaId":"11111111-1111-1111-1111-111111111111",
      "personaNombre":"Juan Perez",
      "puestoId":"22222222-2222-2222-2222-222222222222",
      "puestoNombre":"Analista",
      "fechaInicio":"2026-01-15",
      "fechaFin":null,
      "tipoAsignacion":"Permanente",
      "observaciones":null,
      "estado":"Vigente"
    }
    """;

    // ──────────────────────────────────────────────────
    // CrearAsync
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task CrearAsync_When201_ReturnsSuccessWithDto()
    {
        var newId = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent(SuccessDtoJson(newId))
            });
        var client = BuildClient(handler);

        var result = await client.CrearAsync(SampleCrearRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(newId, result.Value!.Id);
        Assert.Null(result.Error);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal($"{BaseRoute}", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CrearAsync_When409_PreservesConflictCategoriaAndCode()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent("""
                {"title":"PersonaYPuestoOcupados","detail":"El par ya existe.","status":409}
                """)
            });
        var client = BuildClient(handler);

        var result = await client.CrearAsync(SampleCrearRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Conflict, result.Error!.Categoria);
        Assert.Equal("PersonaYPuestoOcupados", result.Error.Code);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task CrearAsync_When400Validation_PopulatesFieldErrors()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent("""
                {
                  "title":"Validation",
                  "status":400,
                  "errors":{
                    "PersonaId":["La persona es obligatoria"],
                    "PuestoId":["El puesto es obligatorio"]
                  }
                }
                """)
            });
        var client = BuildClient(handler);

        var result = await client.CrearAsync(SampleCrearRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
        Assert.NotNull(result.FieldErrors);
        Assert.True(result.FieldErrors!.Count >= 1);
    }

    [Fact]
    public async Task CrearAsync_WhenUnauthorized_CategoriaIsUnauthorized()
    {
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent("""{"title":"Unauthorized","status":401}""")
            });
        var client = BuildClient(handler);

        var result = await client.CrearAsync(SampleCrearRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategoria.Unauthorized, result.Error!.Categoria);
    }

    // ──────────────────────────────────────────────────
    // ActualizarAsync
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task ActualizarAsync_When200_ReturnsSuccessWithDto()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(SuccessDtoJson(id))
            });
        var client = BuildClient(handler);

        var request = new ActualizarOcupacionRequest(
            PersonaId: Guid.NewGuid(),
            PuestoId: Guid.NewGuid(),
            FechaInicio: new DateOnly(2026, 2, 1),
            TipoAsignacion: OcupacionTipoAsignacion.Interina,
            Observaciones: "rotada");

        var result = await client.ActualizarAsync(id, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal($"{BaseRoute}/{id:D}", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ActualizarAsync_When409PuestoOcupado_PreservesConflictCode()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent("""
                {"title":"PuestoOcupado","detail":"El puesto ya tiene otra ocupación vigente.","status":409}
                """)
            });
        var client = BuildClient(handler);

        var request = new ActualizarOcupacionRequest(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 2, 1),
            OcupacionTipoAsignacion.Interina);

        var result = await client.ActualizarAsync(id, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, result.Error!.Categoria);
        Assert.Equal("PuestoOcupado", result.Error.Code);
    }

    // ──────────────────────────────────────────────────
    // FinalizarAsync
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task FinalizarAsync_When200_ReturnsSuccessWithFinalizadaEstado()
    {
        var id = Guid.NewGuid();
        var body = $$"""
        {
          "id":"{{id:D}}",
          "personaId":"11111111-1111-1111-1111-111111111111",
          "personaNombre":"Juan Perez",
          "puestoId":"22222222-2222-2222-2222-222222222222",
          "puestoNombre":"Analista",
          "fechaInicio":"2026-01-15",
          "fechaFin":"2026-06-30",
          "tipoAsignacion":"Permanente",
          "observaciones":null,
          "estado":"Finalizada"
        }
        """;
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(body)
            });
        var client = BuildClient(handler);

        var request = new FinalizarOcupacionRequest(
            FechaFin: new DateOnly(2026, 6, 30),
            Observaciones: null);

        var result = await client.FinalizarAsync(id, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(OcupacionEstado.Finalizada, result.Value!.Estado);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal($"{BaseRoute}/{id:D}/finalizar", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task FinalizarAsync_When400FechaFinInvalida_ReturnsValidation()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent("""
                {"title":"FechaFinInvalida","detail":"FechaFin debe ser >= FechaInicio.","status":400}
                """)
            });
        var client = BuildClient(handler);

        var request = new FinalizarOcupacionRequest(
            FechaFin: new DateOnly(2026, 1, 1),
            Observaciones: null);

        var result = await client.FinalizarAsync(id, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
    }

    // ──────────────────────────────────────────────────
    // EliminarAsync
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task EliminarAsync_When204_ReturnsSuccessWithNullValue()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = BuildClient(handler);

        var result = await client.EliminarAsync(id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.Error);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal($"{BaseRoute}/{id:D}", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task EliminarAsync_When404_ReturnsNotFoundCategoria()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent("""{"title":"NotFound","status":404}""")
            });
        var client = BuildClient(handler);

        var result = await client.EliminarAsync(id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, result.Error!.Categoria);
    }

    [Fact]
    public async Task EliminarAsync_When409_ReturnsConflictCategoria()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent("""
                {"title":"OcupacionNoEditable","detail":"No se puede eliminar.","status":409}
                """)
            });
        var client = BuildClient(handler);

        var result = await client.EliminarAsync(id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, result.Error!.Categoria);
        Assert.Equal("OcupacionNoEditable", result.Error.Code);
    }

    // ──────────────────────────────────────────────────
    // ReactivarAsync
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task ReactivarAsync_When200_ReturnsSuccessWithVigenteEstado()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(SuccessDtoJson(id))
            });
        var client = BuildClient(handler);

        var result = await client.ReactivarAsync(id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(OcupacionEstado.Vigente, result.Value!.Estado);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal($"{BaseRoute}/{id:D}/reactivar", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ReactivarAsync_When409OcupacionYaActiva_PreservesConflictCode()
    {
        var id = Guid.NewGuid();
        var handler = HttpClientExceptionScenarios.NewRecordingHandler(
            req => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent("""
                {"title":"OcupacionYaActiva","detail":"La ocupación ya está vigente.","status":409}
                """)
            });
        var client = BuildClient(handler);

        var result = await client.ReactivarAsync(id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, result.Error!.Categoria);
        Assert.Equal("OcupacionYaActiva", result.Error.Code);
    }

    // ──────────────────────────────────────────────────
    // Transporte: las mutaciones propagan excepciones nativas
    // ──────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task CrearAsync_TransportFails_PropagatesNativeException(
        string scenario, Func<Exception> factory, Type expectedExceptionType)
    {
        _ = scenario;
        var handler = HttpClientExceptionScenarios.NewHandlerThrowing(factory);
        var client = BuildClient(handler);

        var actual = await Record.ExceptionAsync(() => client.CrearAsync(SampleCrearRequest(), CancellationToken.None));
        Assert.NotNull(actual);
        Assert.IsType(expectedExceptionType, actual);
    }
}