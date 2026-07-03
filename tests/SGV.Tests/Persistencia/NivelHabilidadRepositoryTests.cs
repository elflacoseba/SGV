using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Persistence tests for NivelHabilidadRepository (NivelesHabilidad catalog).
/// </summary>
public sealed class NivelHabilidadRepositoryTests
{
    [MySqlFact]
    public async Task ListAllAsync_RetornaNivelesHabilidadSembrados()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new NivelHabilidadRepository(context);

        var niveles = await repo.ListAllAsync(default);

        // Seed data creates 4 niveles: Basico, Intermedio, Avanzado, Experto
        Assert.NotEmpty(niveles);
        Assert.All(niveles, n => Assert.NotNull(n.Codigo));
    }

    [MySqlFact]
    public async Task ListAllAsync_RetornaNivelesOrdenadosPorOrden()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new NivelHabilidadRepository(context);

        var niveles = await repo.ListAllAsync(default);

        Assert.NotEmpty(niveles);
        for (var i = 1; i < niveles.Count; i++)
        {
            Assert.True(niveles[i - 1].Orden <= niveles[i].Orden,
                $"Niveles no quedaron ordenados por Orden ascendente. " +
                $"Posición {i - 1}: Orden={niveles[i - 1].Orden}, " +
                $"Posición {i}: Orden={niveles[i].Orden}");
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_NivelExistente_RetornaNivel()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new NivelHabilidadRepository(context);

        var nivel = await repo.GetByIdAsync(DatosSemilla.NivelBasicoId, default);

        Assert.NotNull(nivel);
        Assert.Equal("Basico", nivel!.Codigo);
        Assert.Equal(1, nivel.ValorNumerico);
    }

    [MySqlFact]
    public async Task GetByIdAsync_NivelInexistente_RetornaNull()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new NivelHabilidadRepository(context);

        var nivel = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(nivel);
    }
}
