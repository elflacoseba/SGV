using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using SGV.Tests.Web._Shared;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
using Xunit;

namespace SGV.Tests.Web.Usuario;

[Collection("WebIntegration")]
public sealed class PersonaBuscadorModalTests
{
    private readonly WebIntegrationFixture _fixture;

    public PersonaBuscadorModalTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PersonaBuscadorModal_TieneRoleDialogYAriaModal()
    {
        await using var lease = await CreateLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(
            @"<div(?=[^>]*id=""usuario-persona-buscador-modal"")(?=[^>]*role=""dialog"")(?=[^>]*aria-modal=""true"")(?=[^>]*aria-labelledby=""usuario-persona-buscador-modal-label"")[^>]*>",
            content);
        Assert.Contains("id=\"usuario-persona-buscador-modal-label\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersonaBuscadorModal_EstadoInicial_MuestraMensajeGuia()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ingresá un texto para buscar personas.", content, StringComparison.Ordinal);
        var availabilityQuery = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal(1, availabilityQuery.PageSize);
    }

    [Fact]
    public async Task PersonaBuscadorModal_EstadoEmpty_MuestraMensajeSinResultados()
    {
        await using var lease = await CreateLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron personas con ese criterio.", content, StringComparison.Ordinal);
        Assert.Contains("data-usuario-persona-estado-empty", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersonaBuscadorModal_ConsultaSameOrigin_UsaClienteTipadoDePersonas()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?search=garcia&soloSinUsuario=true&p=2&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal("garcia", query.Search);
        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.True(query.SoloSinUsuario);
    }

    // ──────────────────────────────────────────────────
    // REQ-USB-12: el modal de Usuarios NO declara
    // `data-solo-sin-usuario`; el JS por defecto debe seguir enviando
    // `soloSinUsuario=true` (back-compat estricta con PR-3 del change
    // 2026-07-17-buscador-personas-modal).
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task PersonaBuscadorModal_Usuarios_NoDeclaraDataSoloSinUsuarioYDefaultSigueSiendoTrue()
    {
        await using var lease = await CreateLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El modal root NO debe declarar el atributo en Usuarios.
        var modalMatch = Regex.Match(
            content,
            @"<div(?=[^>]*id=""usuario-persona-buscador-modal"")[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(modalMatch.Success, "Modal root must be present in /seguridad/usuarios/crear.");
        Assert.DoesNotContain("data-solo-sin-usuario", modalMatch.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifica que el source de <c>usuario-persona-buscador.js</c> ya no
    /// hardcodea <c>searchParams.set('soloSinUsuario', 'true')</c> y que
    /// lee el atributo <c>data-solo-sin-usuario</c> con parseo
    /// case-insensitive. Esto protege REQ-USB-12 / OCC-PER-BUSC-03 sin
    /// requerir jsdom.
    /// </summary>
    [Fact]
    public void PersonaBuscadorModal_JsSource_NoHardcodeaSoloSinUsuarioYLeeAtributo()
    {
        var jsPath = ResolveJsPath(out _);

        Assert.True(File.Exists(jsPath), $"JS source not found at {jsPath}.");
        var source = File.ReadAllText(jsPath);

        // El hardcode de `soloSinUsuario` con literal `'true'` debe haber
        // desaparecido; el valor ahora se deriva del atributo del modal.
        Assert.DoesNotContain(
            "searchParams.set('soloSinUsuario', 'true')",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "searchParams.set(\"soloSinUsuario\", \"true\")",
            source,
            StringComparison.Ordinal);

        // El JS debe leer el atributo case-insensitive contra `"true"`.
        Assert.Contains("data-solo-sin-usuario", source, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(
            new Regex(@"toLowerCase\(\)|toUpperCase\(\)", RegexOptions.IgnoreCase),
            source);
    }

    /// <summary>
    /// Resuelve la ruta absoluta al source de <c>usuario-persona-buscador.js</c>
    /// buscando hacia arriba desde <see cref="AppContext.BaseDirectory"/>
    /// hasta encontrar el archivo. Esto evita depender del output de build
    /// del test (que copia wwwroot pero no siempre según la config del csproj).
    /// </summary>
    private static string ResolveJsPath(out string[] candidates)
    {
        var candidatesLocal = new List<string>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "src", "SGV.Web", "wwwroot", "js", "pages", "usuario-persona-buscador.js");
            candidatesLocal.Add(path);
            if (File.Exists(path))
            {
                candidates = candidatesLocal.ToArray();
                return path;
            }
            dir = dir.Parent;
        }
        candidates = candidatesLocal.ToArray();
        return candidatesLocal[^1];
    }

    [Fact]
    public async Task BFF_BuscarConSearchDe200CaracteresASCII_ReenviaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var search = new string('a', 200);
        var response = await lease.Client.GetAsync(
            $"/api/v1/personas/consulta?p=1&pageSize=10&search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(personaApiClient.QueryCalls);
        var query = personaApiClient.QueryCalls[0];
        Assert.NotNull(query.Search);
        Assert.Equal(200, query.Search!.Length);
    }

    [Fact]
    public async Task BFF_BuscarConSearchDe201CaracteresASCII_Responde400YNoLlamaCliente()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var search = new string('a', 201);
        var response = await lease.Client.GetAsync(
            $"/api/v1/personas/consulta?p=1&pageSize=10&search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(personaApiClient.QueryCalls);
        var detail = await response.Content.ReadAsStringAsync();
        Assert.Contains("200", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bytes", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BFF_BuscarConSearch50Emojis_200Bytes_PasaElCap()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var emoji = "\U0001F600"; // 😀, 4 UTF-8 bytes
        var search = string.Concat(Enumerable.Repeat(emoji, 50));
        Assert.Equal(200, Encoding.UTF8.GetByteCount(search));
        var response = await lease.Client.GetAsync(
            $"/api/v1/personas/consulta?p=1&pageSize=10&search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(personaApiClient.QueryCalls);
    }

    [Fact]
    public async Task BFF_BuscarConSearch51Emojis_204Bytes_Responde400()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var emoji = "\U0001F600"; // 😀, 4 UTF-8 bytes
        var search = string.Concat(Enumerable.Repeat(emoji, 51));
        Assert.Equal(204, Encoding.UTF8.GetByteCount(search));
        var response = await lease.Client.GetAsync(
            $"/api/v1/personas/consulta?p=1&pageSize=10&search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(personaApiClient.QueryCalls);
    }

    [Fact]
    public async Task BFF_BuscarConSortEmailDesc_PropagaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&sort=email_desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal("email_desc", query.Sort);
        Assert.Equal(
            SGV.Contracts.Personas.Consultas.Dtos.PersonaSegmentoListado.Activas,
            query.Segmento);
    }

    [Fact]
    public async Task BFF_BuscarConSortDocumentoAsc_PropagaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&sort=documento_asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal("documento_asc", query.Sort);
    }

    [Fact]
    public async Task BFF_BuscarConSortDocumentoDesc_PropagaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&sort=documento_desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal("documento_desc", query.Sort);
    }

    [Fact]
    public async Task BFF_BuscarConSortInvalido_Responde400YNoLlamaCliente()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&sort=hack");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(personaApiClient.QueryCalls);
    }

    [Fact]
    public async Task BFF_BuscarConSegmentoEliminadas_PropagaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&segmento=eliminadas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal("apellidos_asc", query.Sort);
        Assert.Equal(
            SGV.Contracts.Personas.Consultas.Dtos.PersonaSegmentoListado.Eliminadas,
            query.Segmento);
    }

    [Fact]
    public async Task BFF_BuscarConSegmentoInvalido_Responde400YNoLlamaCliente()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&segmento=todas");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(personaApiClient.QueryCalls);
        var detail = await response.Content.ReadAsStringAsync();
        Assert.Contains("activas", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eliminadas", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BFF_BuscarSinSortNiSegmento_AplicaDefaultsBackCompat()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(personaApiClient.QueryCalls);
        var query = personaApiClient.QueryCalls[0];
        Assert.Equal("apellidos_asc", query.Sort);
        Assert.Equal(
            SGV.Contracts.Personas.Consultas.Dtos.PersonaSegmentoListado.Activas,
            query.Segmento);
    }

    [Fact]
    public async Task BFF_BuscarConSegmentoActivas_PropagaAlClienteTipado()
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&segmento=activas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal("apellidos_asc", query.Sort);
        Assert.Equal(
            SGV.Contracts.Personas.Consultas.Dtos.PersonaSegmentoListado.Activas,
            query.Segmento);
    }

    [Theory]
    [InlineData("apellidos_asc")]
    [InlineData("apellidos_desc")]
    [InlineData("nombres_asc")]
    [InlineData("nombres_desc")]
    [InlineData("legajo_asc")]
    [InlineData("legajo_desc")]
    [InlineData("email_asc")]
    [InlineData("email_desc")]
    [InlineData("documento_asc")]
    [InlineData("documento_desc")]
    [InlineData("APELLIDOS_ASC")]
    [InlineData("Email_Desc")]
    [InlineData("DOCUMENTO_ASC")]
    [InlineData("Documento_Desc")]
    public async Task BFF_BuscarConSortWhitelist_PropagaAlClienteTipado(string sort)
    {
        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await CreateLeaseAsync(personaApiClient);

        var response = await lease.Client.GetAsync(
            $"/api/v1/personas/consulta?p=1&pageSize=10&sort={Uri.EscapeDataString(sort)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal(sort, query.Sort);
    }

    [Fact]
    public async Task BFF_UpstreamNetworkError_Responde502ConProblemDetailsYLogError()
    {
        var personaApiClient = new FakePersonaApiClient
        {
            QueryException = new HttpRequestException("Simulated upstream network failure")
        };
        var loggerProvider = new RecordingLoggerProvider();
        await using var lease = await CreateLeaseWithLoggerAsync(personaApiClient, loggerProvider);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&search=garcia&sort=apellidos_asc&segmento=activas");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("urn:sgv:errors:bff/upstream-unavailable", root.GetProperty("type").GetString());
        Assert.Equal(502, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
        Assert.NotEqual("urn:sgv:errors:bff/upstream-timeout", root.GetProperty("type").GetString());
        Assert.NotEqual("urn:sgv:errors:bff/client-cancelled", root.GetProperty("type").GetString());

        var errorLog = Assert.Single(loggerProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.NotNull(errorLog.StateDictionary);
        Assert.Equal("garcia", errorLog.StateDictionary!["Search"]);
        Assert.Equal("apellidos_asc", errorLog.StateDictionary["Sort"]);
        Assert.Equal("Activas", errorLog.StateDictionary["Segmento"]);
        Assert.IsType<string>(errorLog.StateDictionary["CorrelationId"]);
        Assert.False(string.IsNullOrWhiteSpace((string)errorLog.StateDictionary["CorrelationId"]!));
        Assert.NotNull(errorLog.Exception);
        Assert.IsType<HttpRequestException>(errorLog.Exception);
    }

    [Fact]
    public async Task BFF_UpstreamTimeout_Responde502ConProblemDetailsDistinguible()
    {
        var personaApiClient = new FakePersonaApiClient
        {
            QueryException = new TaskCanceledException("Simulated upstream timeout")
        };
        var loggerProvider = new RecordingLoggerProvider();
        await using var lease = await CreateLeaseWithLoggerAsync(personaApiClient, loggerProvider);

        var response = await lease.Client.GetAsync(
            "/api/v1/personas/consulta?p=1&pageSize=10&search=garcia&sort=apellidos_asc&segmento=activas");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("urn:sgv:errors:bff/upstream-timeout", root.GetProperty("type").GetString());
        Assert.Equal(502, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
        Assert.NotEqual("urn:sgv:errors:bff/upstream-unavailable", root.GetProperty("type").GetString());
        Assert.NotEqual("urn:sgv:errors:bff/client-cancelled", root.GetProperty("type").GetString());

        var errorLog = Assert.Single(loggerProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.NotNull(errorLog.StateDictionary);
        Assert.Equal("garcia", errorLog.StateDictionary!["Search"]);
        Assert.Equal("apellidos_asc", errorLog.StateDictionary["Sort"]);
        Assert.Equal("Activas", errorLog.StateDictionary["Segmento"]);
        Assert.IsType<string>(errorLog.StateDictionary["CorrelationId"]);
        Assert.NotNull(errorLog.Exception);
        Assert.IsType<TaskCanceledException>(errorLog.Exception);
    }

    private Task<WebClientLease> CreateLeaseAsync(FakePersonaApiClient? personaApiClient = null)
        => _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            personaApiClient ?? new FakePersonaApiClient(),
            adminRole: true);

    private Task<WebClientLease> CreateLeaseWithLoggerAsync(
        FakePersonaApiClient personaApiClient,
        RecordingLoggerProvider loggerProvider)
        => _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            personaApiClient,
            loggerProvider,
            adminRole: true);
}
