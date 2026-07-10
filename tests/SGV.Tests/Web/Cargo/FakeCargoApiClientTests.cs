using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;
using CargoListQuery = SGV.Web.Integration.Organizacion.CargoListQuery;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Unit tests for the in-memory <see cref="FakeCargoApiClient"/>'s
/// <c>QueryAsync</c> behavior — specifically its handling of the
/// <c>query.Status</c> segment filter (activas / eliminadas).
/// </summary>
public class FakeCargoApiClientTests
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
        // AC #2 + #5 + #6: el segmento Status se respeta case-insensitively
        // para "activas"/"eliminadas" y, si está ausente (null), cae al
        // snapshot activo por paridad con GetAllAsync.
        var activo = new CargoDto(Guid.NewGuid(), "C-001", "Analista", "Desc", Guid.NewGuid(), "Junior");
        var eliminado = new CargoDto(Guid.NewGuid(), "C-DEL", "Eliminado", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(activo, eliminado);

        await apiClient.DeleteAsync(eliminado.Id);

        var result = await apiClient.QueryAsync(new CargoListQuery(1, 20, null, null, status));

        Assert.Single(result.Items);
        var expected = expectsActiva ? activo : eliminado;
        Assert.Equal(expected.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task IsDeleted_AfterDeleteAsync_ReturnsTrue()
    {
        // AC #7: expose IsDeleted(Guid) so tests can seed soft-deleted
        // state without going through DeleteAsync (useful for Reactivate
        // tests where the entry was already deleted server-side).
        var cargo = new CargoDto(Guid.NewGuid(), "C-001", "Analista", null, Guid.NewGuid(), "Junior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);

        Assert.False(apiClient.IsDeleted(cargo.Id));

        await apiClient.DeleteAsync(cargo.Id);

        Assert.True(apiClient.IsDeleted(cargo.Id));
    }
}