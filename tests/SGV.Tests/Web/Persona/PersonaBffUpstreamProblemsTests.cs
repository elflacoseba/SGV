using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Unit tests para <see cref="PersonaBffUpstreamProblems"/>. Cubre las
/// tres ramas de clasificación (network / timeout / client cancelled) y
/// valida el contrato observable del helper: URN <c>type</c> estable,
/// status 502, content-type <c>application/problem+json</c>, scope
/// estructurado en el log con <c>Search</c>/<c>Sort</c>/<c>Segmento</c>/
/// <c>CorrelationId</c>.
/// </summary>
/// <remarks>
/// La rama "client cancelled" no se testea vía el endpoint integrado
/// porque <c>WebApplicationFactory</c>/TestHost aborta el stream de
/// respuesta cuando el <see cref="CancellationToken"/> del cliente ya
/// estaba cancelado, lo que hace que <c>HttpClient.SendAsync</c> tire
/// <see cref="TaskCanceledException"/> antes de que la pipeline pueda
/// entregar la respuesta 502. El helper, en cambio, es unit-testeable
/// directamente con un <see cref="DefaultHttpContext"/> sintético y un
/// token ya cancelado.
/// </remarks>
public sealed class PersonaBffUpstreamProblemsTests
{
    [Fact]
    public void Build_HttpRequestExceptionSinCancel_Devuelve502ConTipoUpstreamUnavailable()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var logger = loggerProvider.CreateLogger("SGV.Web.Personas.BffUpstream");
        var httpContext = NewHttpContext();
        var query = NewQuery();

        var result = PersonaBffUpstreamProblems.Build(
            httpContext, logger, query, new HttpRequestException("boom"), clientCancelled: false);

        AssertProblem(result, expectedType: PersonaBffUpstreamProblems.UpstreamUnavailableType);
        var entry = Assert.Single(loggerProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Equal("UpstreamUnavailable", entry.StateDictionary!["UpstreamErrorKind"]);
        Assert.IsType<HttpRequestException>(entry.Exception);
    }

    [Fact]
    public void Build_TaskCanceledExceptionSinCancel_Devuelve502ConTipoUpstreamTimeout()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var logger = loggerProvider.CreateLogger("SGV.Web.Personas.BffUpstream");
        var httpContext = NewHttpContext();
        var query = NewQuery();

        var result = PersonaBffUpstreamProblems.Build(
            httpContext, logger, query, new TaskCanceledException("timeout"), clientCancelled: false);

        AssertProblem(result, expectedType: PersonaBffUpstreamProblems.UpstreamTimeoutType);
        var entry = Assert.Single(loggerProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Equal("UpstreamTimeout", entry.StateDictionary!["UpstreamErrorKind"]);
        Assert.IsType<TaskCanceledException>(entry.Exception);
    }

    [Fact]
    public void Build_TaskCanceledExceptionConCancel_Devuelve502ConTipoClientCancelled()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var logger = loggerProvider.CreateLogger("SGV.Web.Personas.BffUpstream");
        var httpContext = NewHttpContext();
        var query = NewQuery();

        var result = PersonaBffUpstreamProblems.Build(
            httpContext, logger, query, new TaskCanceledException("client aborted"), clientCancelled: true);

        AssertProblem(result, expectedType: PersonaBffUpstreamProblems.ClientCancelledType);
        var entry = Assert.Single(loggerProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Equal("ClientCancelled", entry.StateDictionary!["UpstreamErrorKind"]);
    }

    [Fact]
    public void Build_LogScopeContieneSearchSortSegmentoCorrelationId()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var logger = loggerProvider.CreateLogger("SGV.Web.Personas.BffUpstream");
        var httpContext = NewHttpContext();
        var query = new PersonaListQuery(
            Page: 1,
            PageSize: 25,
            Search: "garcia",
            Sort: "apellidos_asc",
            Segmento: PersonaSegmentoListado.Eliminadas);

        PersonaBffUpstreamProblems.Build(
            httpContext, logger, query, new HttpRequestException("boom"), clientCancelled: false);

        var entry = Assert.Single(loggerProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.NotNull(entry.StateDictionary);
        Assert.Equal("garcia", entry.StateDictionary!["Search"]);
        Assert.Equal("apellidos_asc", entry.StateDictionary["Sort"]);
        Assert.Equal("Eliminadas", entry.StateDictionary["Segmento"]);
        Assert.Equal(httpContext.TraceIdentifier, entry.StateDictionary["CorrelationId"]);
    }

    private static void AssertProblem(IResult result, string expectedType)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = NewServiceProvider()
        };
        var stream = new MemoryStream();
        httpContext.Response.Body = stream;

        result.ExecuteAsync(httpContext).GetAwaiter().GetResult();

        Assert.Equal(StatusCodes.Status502BadGateway, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        Assert.Equal(expectedType, root.GetProperty("type").GetString());
        Assert.Equal(502, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
    }

    private static IServiceProvider NewServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.Configure<JsonOptions>(o => { });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext NewHttpContext()
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = NewServiceProvider()
        };
        ctx.Request.Path = "/api/v1/personas/consulta";
        ctx.TraceIdentifier = "trace-164-test";
        return ctx;
    }

    private static PersonaListQuery NewQuery() =>
        new(Page: 1, PageSize: 25, Search: "garcia", Sort: "apellidos_asc", Segmento: PersonaSegmentoListado.Activas);
}