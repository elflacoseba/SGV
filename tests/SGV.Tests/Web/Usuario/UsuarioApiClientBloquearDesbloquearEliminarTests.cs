using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Usuarios;
using Xunit;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Tests del seam HTTP para los endpoints de lockout admin del módulo
/// usuarios introducidos por el change
/// <c>2026-07-15-quita-soft-delete-usuario</c> (Phase 3, slice web).
/// Cubre el wire hacia <c>POST /api/v1/usuarios/{id}/bloquear</c>,
/// <c>POST /api/v1/usuarios/{id}/desbloquear</c> y
/// <c>DELETE /api/v1/usuarios/{id}</c> (hard-delete del usuario).
/// </summary>
public class UsuarioApiClientBloquearDesbloquearEliminarTests
{
    [Fact]
    public async Task BloquearAsync_Http200_ReturnsSuccessAndHitsBloquearRoute()
    {
        // AC: el bloque admin devuelve 200 con el DTO actualizado para
        // que la Razor Page pueda mostrar el nuevo estado (Bloqueado=true).
        var personaId = Guid.NewGuid();
        var payload = new UsuarioDto(
            "u-lock", personaId, "locked", "locked@example.com",
            new[] { "Consultor" }, Nombres: "L", Apellidos: "Locked", Bloqueado: true);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.BloquearAsync("u-lock");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Bloqueado);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/usuarios/u-lock/bloquear", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task BloquearAsync_Http403AutoBloqueo_ReturnsFailureWithForbiddenCategoria()
    {
        // AC: AutoBloqueo se traduce a Forbidden con Code="AutoBloqueo"
        // para que el banner sea accionable en el shell web.
        var problem = new ProblemDetails
        {
            Status = 403,
            Title = "AutoBloqueo",
            Detail = "No puede bloquear su propio usuario."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Forbidden, problem));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.BloquearAsync("u-self");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Forbidden, result.Error!.Categoria);
        Assert.Equal("AutoBloqueo", result.Error.Code);
    }

    [Fact]
    public async Task DesbloquearAsync_Http200_ReturnsSuccessAndHitsDesbloquearRoute()
    {
        var personaId = Guid.NewGuid();
        var payload = new UsuarioDto(
            "u-unlock", personaId, "unlocked", "u@example.com",
            new[] { "Consultor" }, Nombres: "U", Apellidos: "Unlocked", Bloqueado: false);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.DesbloquearAsync("u-unlock");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Bloqueado);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/usuarios/u-unlock/desbloquear", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task EliminarAsync_Http204_ReturnsSuccessAndHitsDeleteRoute()
    {
        // AC: hard-delete devuelve 204 sin body. El cliente tipado
        // detecta 204 como éxito y materializa Success(null) para no
        // propagar excepciones ni falsos fallos. La Razor Page de Index
        // (Phase 3) redirige a la vista activas después del alta.
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.EliminarAsync("u-delete");

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/usuarios/u-delete", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task EliminarAsync_Http403AutoEliminacion_ReturnsFailureWithForbiddenCategoria()
    {
        // AC: AutoEliminacion se traduce a Forbidden con Code="AutoEliminacion"
        // para que el banner sea accionable en el shell web.
        var problem = new ProblemDetails
        {
            Status = 403,
            Title = "AutoEliminacion",
            Detail = "No puede eliminar su propio usuario."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Forbidden, problem));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.EliminarAsync("u-self");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.Forbidden, result.Error!.Categoria);
        Assert.Equal("AutoEliminacion", result.Error.Code);
    }

    [Fact]
    public async Task EliminarAsync_Http404_ReturnsFailureWithNotFoundCategoria()
    {
        // AC: doble eliminación es un 404 idempotente (la segunda
        // operación encuentra el recurso ya borrado). El cliente debe
        // traducirlo a Failure con Categoria=NotFound para que el
        // PageModel muestre feedback accionable.
        var problem = new ProblemDetails
        {
            Status = 404,
            Title = "UsuarioNoEncontrado",
            Detail = "El usuario no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new UsuarioApiClient(NewHttpClient(handler));

        var result = await client.EliminarAsync("u-gone");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorCategoria.NotFound, result.Error!.Categoria);
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        return new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(payload)
        };
    }
}
