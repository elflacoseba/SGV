using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Infraestructura.Organizacion;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Infraestructura.Organizacion;

/// <summary>
/// Tests for the cycle diagnostic service introduced by issue #277. The
/// service is invoked once at startup by <c>Program.cs</c> (and on demand
/// by operators); it must report pre-existing cycles without mutating any
/// row. All scenarios live behind <see cref="MySqlFactAttribute"/> because
/// they require a real MySQL connection (the diagnostics read directly from
/// the persistence layer).
/// </summary>
public sealed class DiagnosticoJerarquiaServiceTests
{
    [MySqlFact]
    public async Task DiagnosticarAsync_SinCiclos_RetornaListaVacia()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var r = RepositoryTestData.CreateUnidadOrganizativa("DIAG-R");
        var x = RepositoryTestData.CreateUnidadOrganizativa("DIAG-X");
        x.UnidadPadreId = r.Id;
        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([r, x]);
        await context.SaveChangesAsync();

        try
        {
            var sut = new DiagnosticoJerarquiaService(context);
            var ciclos = await sut.DiagnosticarAsync(default);
            Assert.Empty(ciclos);
        }
        finally
        {
            // Clear padre first to satisfy CK constraint before deletion.
            x.UnidadPadreId = null;
            await context.SaveChangesAsync();
            context.Set<UnidadOrganizativaEntity>().RemoveRange(r, x);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DiagnosticarAsync_ConCiclo_RetornaCadaCicloDetectado()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var a = RepositoryTestData.CreateUnidadOrganizativa("DIAG-A");
        var b = RepositoryTestData.CreateUnidadOrganizativa("DIAG-B");
        a.UnidadPadreId = b.Id;
        b.UnidadPadreId = a.Id;
        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([a, b]);
        await context.SaveChangesAsync();

        try
        {
            var sut = new DiagnosticoJerarquiaService(context);
            var ciclos = await sut.DiagnosticarAsync(default);

            Assert.NotEmpty(ciclos);
            // El ciclo A↔B debería reportar al menos un CicloDetectado que
            // mencione ambos nodos. La forma exacta del path puede variar
            // según el orden de iteración (A→B→A o B→A→B).
            Assert.Contains(ciclos, c => c.Nodos.Contains(a.Id) && c.Nodos.Contains(b.Id));
        }
        finally
        {
            a.UnidadPadreId = null;
            b.UnidadPadreId = null;
            await context.SaveChangesAsync();
            context.Set<UnidadOrganizativaEntity>().RemoveRange(a, b);
            await context.SaveChangesAsync();
        }
    }
}
