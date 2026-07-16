using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Unit tests para el comportamiento del fake en memoria
/// <see cref="FakeUsuarioApiClient"/>: específicamente el manejo del
/// segmento <see cref="UsuarioSegmentoListado"/> (activas / eliminadas),
/// la búsqueda cross-field y el orden por defecto. Espejo de
/// <c>FakePersonaApiClientTests</c> adaptado al shape Identity (id es
/// string) y al dominio Usuarios (roles + name keys).
/// </summary>
public class FakeUsuarioApiClientTests
{
    [Theory]
    [InlineData(UsuarioSegmentoListado.Activas)]
    [InlineData(UsuarioSegmentoListado.Eliminadas)]
    public async Task QueryAsync_WithSegmento_ReturnsExpectedSubset(UsuarioSegmentoListado segmento)
    {
        // AC: el segmento Activas/Eliminadas filtra exactamente sobre la
        // marca de baja lógica interna del fake.
        var activa = BuildUsuario("u-active", "u-active@example.com", activo: true);
        var eliminada = BuildUsuario("u-deleted", "u-deleted@example.com", activo: true);
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(activa, eliminada);

        await apiClient.DesactivarAsync(eliminada.Id);

        var result = await apiClient.QueryAsync(new UsuarioListQuery(1, 20, null, null, segmento));

        if (segmento == UsuarioSegmentoListado.Activas)
        {
            Assert.Single(result.Result.Items);
            Assert.Equal(activa.Id, result.Result.Items[0].Id);
        }
        else
        {
            Assert.Single(result.Result.Items);
            Assert.Equal(eliminada.Id, result.Result.Items[0].Id);
        }
    }

    [Fact]
    public async Task IsDeleted_AfterDesactivarAsync_ReturnsTrue()
    {
        var usuario = BuildUsuario("u-mark", "u-mark@example.com");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        Assert.False(apiClient.IsDeleted(usuario.Id));

        await apiClient.DesactivarAsync(usuario.Id);

        Assert.True(apiClient.IsDeleted(usuario.Id));
    }

    [Fact]
    public async Task QueryAsync_WithSearchFilterAcrossFiveFields_AppliesCaseInsensitiveSubstring()
    {
        // REQ identity-user-role-management / Listado paginado: la
        // búsqueda aplica a UserName|Email|Nombres|Apellidos
        // case-insensitive (la PersonaId es Guid y no aplica
        // Substring). Triangulamos tres campos diferentes con el
        // mismo término para validar la cobertura completa.
        var ana = BuildUsuario("agarcía", "ana@example.com", nombres: "Ana", apellidos: "García");
        var juan = BuildUsuario("jperez", "juan@example.com", nombres: "Juan", apellidos: "Pérez");
        var maria = BuildUsuario("mlopez", "maria@example.com", nombres: "María", apellidos: "García");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(ana, juan, maria);

        // Búsqueda por userName
        var byUserName = await apiClient.QueryAsync(new UsuarioListQuery(1, 20, "jperez", null, UsuarioSegmentoListado.Activas));
        Assert.Single(byUserName.Result.Items);
        Assert.Equal(juan.Id, byUserName.Result.Items[0].Id);

        // Búsqueda por email
        var byEmail = await apiClient.QueryAsync(new UsuarioListQuery(1, 20, "ANA@EXAMPLE", null, UsuarioSegmentoListado.Activas));
        Assert.Single(byEmail.Result.Items);
        Assert.Equal(ana.Id, byEmail.Result.Items[0].Id);

        // Búsqueda por apellido (compartido entre ana y maria)
        var byApellido = await apiClient.QueryAsync(new UsuarioListQuery(1, 20, "GARCÍA", null, UsuarioSegmentoListado.Activas));
        Assert.Equal(2, byApellido.Result.Items.Count);
    }

    [Fact]
    public async Task QueryAsync_DefaultSort_OrdersByUserNameAscending()
    {
        // AC: cuando no se especifica sort, el fake cae a userName_asc
        // (consistente con la convención del backend de Usuarios).
        var ana = BuildUsuario("z", "z@example.com");
        var juan = BuildUsuario("a", "a@example.com");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(ana, juan);

        var result = await apiClient.QueryAsync(new UsuarioListQuery(1, 20, null, null, UsuarioSegmentoListado.Activas));

        Assert.Equal(juan.Id, result.Result.Items[0].Id);
        Assert.Equal(ana.Id, result.Result.Items[1].Id);
    }

    [Fact]
    public async Task ReactivarAsync_AfterDesactivarAsync_MovesUserBackToActivas()
    {
        // AC: tras DesactivarAsync + ReactivarAsync el id vuelve al
        // segmento Activas. Espejo del patrón FakePersonaApiClient pero
        // para usuarios.
        var usuario = BuildUsuario("u-cycle", "cycle@example.com");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await apiClient.DesactivarAsync(usuario.Id);
        Assert.True(apiClient.IsDeleted(usuario.Id));

        await apiClient.ReactivarAsync(usuario.Id);
        Assert.False(apiClient.IsDeleted(usuario.Id));

        var result = await apiClient.QueryAsync(new UsuarioListQuery(1, 20, null, null, UsuarioSegmentoListado.Activas));
        Assert.Single(result.Result.Items);
        Assert.Equal(usuario.Id, result.Result.Items[0].Id);
    }

    internal static UsuarioDto BuildUsuario(string userName, string email, bool activo = true, string? nombres = null, string? apellidos = null)
        => new(
            Id: $"u-{Guid.NewGuid():N}",
            PersonaId: Guid.NewGuid(),
            UserName: userName,
            Email: email,
            Roles: new[] { "Consultor" },
            Nombres: nombres,
            Apellidos: apellidos);
}
