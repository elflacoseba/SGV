using System.Net;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Web.Integration.Common;
using Xunit;

namespace SGV.Tests.Web.Common;

/// <summary>
/// Tests parametrizados del helper <see cref="CommandResultMapper.Map"/>
/// introducido en #125 (Slice 2). Cubren cada fila de la matriz REQ-2 más
/// cinco status atípicos para asegurar que el fallback <c>Unexpected</c>
/// preserva el status code como metadata de diagnóstico.
/// </summary>
public class CommandResultMapperTests
{
    // ──────────────────────────────────────────────
    // Matriz REQ-2 (status explícitos)
    // ──────────────────────────────────────────────

    [Fact]
    public void Map_Status400_WithoutFieldErrors_ReturnsValidationWithBadRequestDefault()
    {
        // Body ProblemDetails plano → code "BadRequest", message "Solicitud inválida."
        // (sin FieldErrors → rama Validation sin detail por campo).
        var response = BuildResponse(HttpStatusCode.BadRequest, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.BadRequest, Title: null, Detail: null, FieldErrors: null);

        var (categoria, code, message, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Validation, categoria);
        Assert.Equal("BadRequest", code);
        Assert.Equal("Solicitud inválida.", message);
        Assert.Equal(400, status);
    }

    [Fact]
    public void Map_Status400_WithFieldErrors_PreservesParsedTitleAndDetail()
    {
        var response = BuildResponse(HttpStatusCode.BadRequest, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.BadRequest,
            Title: "CodigoDuplicado",
            Detail: "Ya existe un cargo activo con ese código.",
            FieldErrors: new Dictionary<string, string[]> { ["codigo"] = ["duplicado"] });

        var (categoria, code, message, _) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Validation, categoria);
        Assert.Equal("CodigoDuplicado", code);
        Assert.Equal("Ya existe un cargo activo con ese código.", message);
    }

    [Fact]
    public void Map_Status401_ReturnsUnauthorizedWithDefaultMessage()
    {
        var response = BuildResponse(HttpStatusCode.Unauthorized, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.Unauthorized, Title: null, Detail: null, FieldErrors: null);

        var (categoria, code, message, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Unauthorized, categoria);
        Assert.Equal("Unauthorized", code);
        Assert.Equal("Su sesión expiró. Vuelva a iniciar sesión.", message);
        Assert.Equal(401, status);
    }

    [Fact]
    public void Map_Status403_ReturnsForbiddenWithAccesoDenegado()
    {
        var response = BuildResponse(HttpStatusCode.Forbidden, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.Forbidden, Title: null, Detail: null, FieldErrors: null);

        var (categoria, code, message, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Forbidden, categoria);
        Assert.Equal("Forbidden", code);
        Assert.Equal("Acceso denegado.", message);
        Assert.Equal(403, status);
    }

    [Fact]
    public void Map_Status404_ReturnsNotFoundWithRecursoNoEncontrado()
    {
        var response = BuildResponse(HttpStatusCode.NotFound, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.NotFound, Title: null, Detail: null, FieldErrors: null);

        var (categoria, code, message, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.NotFound, categoria);
        Assert.Equal("NotFound", code);
        Assert.Equal("Recurso no encontrado.", message);
        Assert.Equal(404, status);
    }

    [Fact]
    public void Map_Status409_ReturnsConflictWithConflicto()
    {
        var response = BuildResponse(HttpStatusCode.Conflict, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.Conflict, Title: null, Detail: null, FieldErrors: null);

        var (categoria, code, message, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Conflict, categoria);
        Assert.Equal("Conflict", code);
        Assert.Equal("Conflicto.", message);
        Assert.Equal(409, status);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void Map_TransportStatusCodes_MapToTransportCategoria(HttpStatusCode transportStatus)
    {
        var response = BuildResponse(transportStatus, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            transportStatus, Title: null, Detail: null, FieldErrors: null);

        var (categoria, code, message, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Transport, categoria);
        Assert.Equal("TransportError", code);
        Assert.Equal("El servicio no respondió correctamente. Intentá nuevamente.", message);
        Assert.Equal((int)transportStatus, status);
    }

    [Fact]
    public void Map_Status422_WithoutFieldErrors_ReturnsValidation()
    {
        // 422 sin errores de campo también cae en Validation (mismo que 400).
        // El status code numérico se preserva verbatim para diagnóstico.
        var response = BuildResponse((HttpStatusCode)422, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            (HttpStatusCode)422, Title: null, Detail: null, FieldErrors: null);

        var (categoria, code, message, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Validation, categoria);
        Assert.Equal("BadRequest", code);
        Assert.Equal(422, status);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    // ──────────────────────────────────────────────
    // Status atípicos (REQ-2 "otro" + extras): caen en Unexpected preservando status.
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData((HttpStatusCode)300, "MultipleChoices")]
    [InlineData((HttpStatusCode)418, "ImATeapot")]
    [InlineData((HttpStatusCode)507, "InsufficientStorage")]
    [InlineData((HttpStatusCode)999, "Unknown")]
    [InlineData((HttpStatusCode)226, "IMUsed")]
    public void Map_AtypicalStatus_MapToUnexpectedPreservingStatus(
        HttpStatusCode atypical, string expectedTitleFragment)
    {
        var response = BuildResponse(atypical, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            atypical, Title: null, Detail: null, FieldErrors: null);

        var (categoria, code, message, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Unexpected, categoria);
        Assert.Equal("Unexpected", code);
        Assert.Equal("Respuesta inesperada del servidor.", message);
        Assert.Equal((int)atypical, status);
    }

    [Fact]
    public void Map_RedirectStatus_MapToUnexpected()
    {
        // 302 redirect no es un error funcional para el cliente HTTP tipado;
        // cae en Unexpected. La categ. FunctionalSurface la muestra como
        // mensaje global genérico.
        var response = BuildResponse(HttpStatusCode.Found, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.Found, Title: null, Detail: null, FieldErrors: null);

        var (categoria, _, _, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Unexpected, categoria);
        Assert.Equal(302, status);
    }

    // ──────────────────────────────────────────────
    // Cuando el ProblemDetails aporta Title/Detail propios, el mapper
    // los prefiere sobre los defaults (preserva magia contractual: el
    // backend entrega CodigoDuplicado, PuestoSuperiorInvalido, etc.).
    // ──────────────────────────────────────────────

    [Fact]
    public void Map_PreferredParsedTitleAndDetail_OverrideDefaults()
    {
        var response = BuildResponse(HttpStatusCode.Conflict, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.Conflict,
            Title: "CodigoDuplicado",
            Detail: "Ya existe un cargo activo con el código C-DUP.",
            FieldErrors: null);

        var (categoria, code, message, _) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(ErrorCategoria.Conflict, categoria);
        Assert.Equal("CodigoDuplicado", code);
        Assert.Equal("Ya existe un cargo activo con el código C-DUP.", message);
    }

    [Fact]
    public void Map_EmptyParsedTitle_FallsBackToDefault()
    {
        // Si parsed.Title es string vacío, el mapper debe usar el default.
        var response = BuildResponse(HttpStatusCode.NotFound, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.NotFound, Title: "", Detail: null, FieldErrors: null);

        var (_, code, _, _) = CommandResultMapper.Map(response, parsed);

        Assert.Equal("NotFound", code);
    }

    [Fact]
    public void Map_ServerSuccess_IsNotMapperInput_RegressionGuard()
    {
        // El mapper está pensado sólo para status no-2xx. Un 200 NO es un
        // input esperado (los clientes llaman al mapper sólo en la rama
        // !IsSuccessStatusCode); aún así, si alguien lo invoca, el status
        // se preserva y la categoría cae en Unexpected (no rompe el switch
        // exhaustivo). Test documenta este comportamiento defensivo.
        var response = BuildResponse(HttpStatusCode.OK, title: null, detail: null);
        var parsed = new ApiProblemReader.Result(
            HttpStatusCode.OK, Title: null, Detail: null, FieldErrors: null);

        var (categoria, _, _, status) = CommandResultMapper.Map(response, parsed);

        Assert.Equal(200, status);
        // 2xx no aparece en la matriz REQ-2; cae en Unexpected por defecto.
        Assert.Equal(ErrorCategoria.Unexpected, categoria);
    }

    private static HttpResponseMessage BuildResponse(
        HttpStatusCode status,
        string? title,
        string? detail)
    {
        var response = new HttpResponseMessage(status);
        // Body vacío: el mapper trabaja sobre el resultado de ApiProblemReader, no sobre el body crudo.
        response.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
        return response;
    }
}
