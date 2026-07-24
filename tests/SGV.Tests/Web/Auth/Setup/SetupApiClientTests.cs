using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Setup;
using SGV.Web.Integration.Setup;
using Xunit;

namespace SGV.Tests.Web.Auth.Setup;

/// <summary>
/// Tests unitarios del typed client <see cref="SetupApiClient"/>
/// (issue #195 / WU-4). Cubren el cache TTL 30s del status, el
/// fail-open ante <see cref="HttpRequestException"/> y
/// <see cref="TaskCanceledException"/>, el mapeo de la respuesta 2xx
/// con <see cref="SetupCommandResult"/>, y el mapeo de errores 4xx/5xx
/// hacia <see cref="SetupHttpResult"/> con códigos de dominio.
/// </summary>
public sealed class SetupApiClientTests
{
    private const string StatusUrl = "/api/v1/setup/status";
    private const string TiposDocumentoUrl = "/api/v1/tipos-documento";
    private const string SetupUrl = "/api/v1/setup";

    private static (SetupApiClient client, RecordingHandler handler, IMemoryCache cache) CreateClient()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new SetupApiClient(http, cache, NullLogger<SetupApiClient>.Instance);
        return (client, handler, cache);
    }

    private static HttpRequestMessage LastRequest(RecordingHandler handler)
    {
        Assert.NotEmpty(handler.Requests);
        return handler.Requests[^1];
    }

    [Fact]
    public async Task ObtenerEstadoAsync_ServidorDevuelveTrue_DevuelveTrue()
    {
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SetupStatusResponse(true))
        });

        var status = await client.ObtenerEstadoAsync();

        Assert.True(status.RequiresSetup);
        Assert.Equal(HttpMethod.Get, LastRequest(handler).Method);
        Assert.Equal(StatusUrl, LastRequest(handler).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_DosLlamadasEnVentanaDe30s_SoloUnaPeticionAlServidor()
    {
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SetupStatusResponse(true))
        });

        var first = await client.ObtenerEstadoAsync();
        var second = await client.ObtenerEstadoAsync();
        var third = await client.ObtenerEstadoAsync();

        Assert.True(first.RequiresSetup);
        Assert.True(second.RequiresSetup);
        Assert.True(third.RequiresSetup);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_FallaHttpRequestException_DevuelveFailOpenFalse()
    {
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new HttpRequestException("connection refused"));

        var status = await client.ObtenerEstadoAsync();

        // Fail-open (design §2.3): cuando la API está caída la Web
        // debe renderizar SignIn en vez de romper el acceso a producción.
        Assert.False(status.RequiresSetup);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_FallaTaskCanceledException_DevuelveFailOpenFalse()
    {
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new TaskCanceledException("timeout"));

        var status = await client.ObtenerEstadoAsync();

        Assert.False(status.RequiresSetup);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_FallaYRecuperacion_RecacheaValorReal()
    {
        var (client, handler, cache) = CreateClient();

        // Primer hit: API caída → fail-open en memoria.
        handler.QueueResponse(new HttpRequestException("connection refused"));
        var fallback = await client.ObtenerEstadoAsync();
        Assert.False(fallback.RequiresSetup);
        Assert.Single(handler.Requests);

        // Limpiamos el cache manualmente para forzar nuevo round-trip.
        cache.Remove(SetupApiClient.StatusCacheKey);

        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SetupStatusResponse(true))
        });
        var recovered = await client.ObtenerEstadoAsync();

        Assert.True(recovered.RequiresSetup);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_ServidorDevuelve5xx_DevuelveFailOpenFalse()
    {
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("oops")
        });

        var status = await client.ObtenerEstadoAsync();

        Assert.False(status.RequiresSetup);
    }

    [Fact]
    public async Task GetTiposDocumentoAsync_ServidorDevuelveLista_DevuelveLista()
    {
        var (client, handler, _) = CreateClient();
        var expected = new List<TipoDocumentoDto>
        {
            new(Guid.Parse("71000000-0000-0000-0000-000000000001"), "DNI", "Documento Nacional", "^\\d{7,8}$", 7, 8),
            new(Guid.Parse("71000000-0000-0000-0000-000000000002"), "PAS", "Pasaporte", null, null, null)
        };
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected)
        });

        var tipos = await client.GetTiposDocumentoAsync();

        Assert.Equal(2, tipos.Count);
        Assert.Equal("DNI", tipos[0].Codigo);
        Assert.Equal(TiposDocumentoUrl, LastRequest(handler).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetTiposDocumentoAsync_ServidorDevuelve5xx_PropagaExcepcion()
    {
        // El catálogo es necesario para hidratar el dropdown del form;
        // si la API cae, queremos que la página muestre el error recuperable
        // y no un fail-open silencioso que deje al usuario sin saber qué
        // tipo de documento elegir.
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("oops")
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTiposDocumentoAsync());
    }

    [Fact]
    public async Task CrearAsync_ServidorDevuelve200_DevuelveSuccessConSetupResult()
    {
        var (client, handler, _) = CreateClient();
        var personaId = Guid.NewGuid();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SetupCommandResult(
                true,
                new SetupResult(personaId, "user-123", "admin"),
                null))
        });

        var request = NewValidRequest();
        var result = await client.CrearAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(personaId, result.Value!.PersonaId);
        Assert.Equal("admin", result.Value.UserName);
        Assert.Equal(HttpMethod.Post, LastRequest(handler).Method);
        Assert.Equal(SetupUrl, LastRequest(handler).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CrearAsync_ServidorDevuelve400_DevuelveFailureConFieldErrors()
    {
        var (client, handler, _) = CreateClient();
        var problem = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"title":"DatosInvalidos","detail":"Datos inválidos","errors":{"Password":["muy corta"],"Email":["inválido"]}}""",
                System.Text.Encoding.UTF8,
                "application/json")
        };
        handler.QueueResponse(problem);

        var result = await client.CrearAsync(NewValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.Error!.StatusCode);
        Assert.Equal(SetupErrorCode.DatosInvalidos, result.Error.Code);
        Assert.NotNull(result.FieldErrors);
        Assert.Equal(2, result.FieldErrors!.Count);
        Assert.Contains("Password", result.FieldErrors.Keys);
        Assert.Contains("Email", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task CrearAsync_ServidorDevuelve409_DevuelveFailureConCodigoConflict()
    {
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"SetupYaCompletado","detail":"La configuración inicial ya fue completada.","statusCode":409}""",
                System.Text.Encoding.UTF8,
                "application/json")
        });

        var result = await client.CrearAsync(NewValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, result.Error!.StatusCode);
        Assert.Equal(SetupErrorCode.SetupYaCompletado, result.Error.Code);
    }

    [Fact]
    public async Task CrearAsync_ServidorDevuelve429_DevuelveFailureConCategoriaTransport()
    {
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("rate limit", System.Text.Encoding.UTF8, "application/json")
        });

        var result = await client.CrearAsync(NewValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.TooManyRequests, result.Error!.StatusCode);
    }

    [Fact]
    public async Task CrearAsync_ServidorDevuelve500_DevuelveFailureConCategoriaTransport()
    {
        var (client, handler, _) = CreateClient();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("oops", System.Text.Encoding.UTF8, "application/json")
        });

        var result = await client.CrearAsync(NewValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.InternalServerError, result.Error!.StatusCode);
    }

    private static SetupRequest NewValidRequest() =>
        new(
            Nombres: "Operador",
            Apellidos: "Inicial",
            Legajo: "LEG-001",
            Email: "admin@setup.test",
            UserName: "admin",
            Password: "Setup#12345",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: "+5491100000000");

    /// <summary>
    /// Handler que encola respuestas para retornar en orden y registra
    /// cada <see cref="HttpRequestMessage"/> recibido. Es el equivalente
    /// minimalista del <c>RecordingHttpMessageHandler</c> en
    /// <c>tests/SGV.Tests/Web/Collections/WebTestBuilders.cs</c> pero
    /// re-instanciable en cada test para evitar estado compartido.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<object> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = new();

        public void QueueResponse(HttpResponseMessage response) => _responses.Enqueue(response);

        public void QueueResponse(Exception exception) => _responses.Enqueue(exception);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No hay respuestas encoladas para {request.Method} {request.RequestUri}");
            }

            var next = _responses.Dequeue();
            if (next is HttpResponseMessage response)
            {
                return response;
            }

            if (next is Exception ex)
            {
                throw ex;
            }

            throw new InvalidOperationException("Tipo inesperado en la cola");
        }
    }
}
