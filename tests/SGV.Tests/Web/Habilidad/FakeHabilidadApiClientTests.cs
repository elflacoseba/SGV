using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using Xunit;
using HabilidadListQuery = SGV.Web.Integration.Habilidades.HabilidadListQuery;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Unit tests for the in-memory <see cref="FakeHabilidadApiClient"/>'s
/// <c>QueryAsync</c> behavior — specifically its handling of the
/// <c>query.Status</c> segment filter (activas / eliminadas).
///
/// Mirrors the pattern of <c>FakeCargoApiClientTests</c>.
/// </summary>
public class FakeHabilidadApiClientTests
{
    [Theory]
    [InlineData("activas", true)]
    [InlineData("ACTIVAS", true)]
    [InlineData("Activas", true)]
    [InlineData("eliminadas", false)]
    [InlineData("ELIMINADAS", false)]
    [InlineData("Eliminadas", false)]
    [InlineData(null, true)] // backwards-compat: null defaults to activas
    public async Task QueryAsync_WithStatusCaseVariants_ReturnsExpectedSegment(string? status, bool expectsActiva)
    {
        // AC #1 + #4 + #6: el segmento Status se respeta case-insensitively
        // para "activas"/"eliminadas" y, si está ausente (null), cae al
        // snapshot activo por paridad con GetAllAsync.
        var activa = new HabilidadDto(Guid.NewGuid(), "H-001", "Liderazgo", null, "Conductual");
        var eliminada = new HabilidadDto(Guid.NewGuid(), "H-DEL", "Habilidad Eliminada", null, "Técnica");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(activa, eliminada);

        await apiClient.DeleteAsync(eliminada.Id);

        var result = await apiClient.QueryAsync(new HabilidadListQuery(1, 20, null, null, status));

        Assert.Single(result.Items);
        var expected = expectsActiva ? activa : eliminada;
        Assert.Equal(expected.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_WithStatusActivasOrEliminadas_FiltersAccordingly()
    {
        // AC #1 + #4: with one active and one deleted habilidad, QueryAsync
        // must respect the Status segment (activas → only the active,
        // eliminadas → only the deleted). Verifica además que el contador
        // total refleje sólo el segmento consultado.
        var activa = new HabilidadDto(Guid.NewGuid(), "H-001", "Liderazgo", "Desc", "Conductual");
        var eliminada = new HabilidadDto(Guid.NewGuid(), "H-DEL", "Habilidad Eliminada", null, "Técnica");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(activa, eliminada);

        await apiClient.DeleteAsync(eliminada.Id);

        var activas = await apiClient.QueryAsync(new HabilidadListQuery(1, 20, null, null, "activas"));
        Assert.Single(activas.Items);
        Assert.Equal(activa.Id, activas.Items[0].Id);
        Assert.Equal(1, activas.TotalCount);

        var eliminadas = await apiClient.QueryAsync(new HabilidadListQuery(1, 20, null, null, "eliminadas"));
        Assert.Single(eliminadas.Items);
        Assert.Equal(eliminada.Id, eliminadas.Items[0].Id);
        Assert.Equal(1, eliminadas.TotalCount);
    }

    [Fact]
    public async Task IsDeleted_AfterDeleteAsync_ReturnsTrue()
    {
        // AC #7: expose IsDeleted(Guid) so tests can seed soft-deleted
        // state without going through DeleteAsync (useful for Reactivate
        // tests where the entry was already deleted server-side).
        var activa = new HabilidadDto(Guid.NewGuid(), "H-001", "Liderazgo", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(activa);

        Assert.False(apiClient.IsDeleted(activa.Id));

        await apiClient.DeleteAsync(activa.Id);

        Assert.True(apiClient.IsDeleted(activa.Id));
    }
}