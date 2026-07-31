using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Verifies that <see cref="EstadoVacanteConstantes"/> contains exactly 4
/// unique, non-empty Guids (the bloque <c>20000000-…</c> reserved in
/// <c>docs/decisiones-implementacion.md</c>) and that the seed values
/// match the canonical 4 estados (Abierta, EnSeleccion, Cubierta,
/// Cancelada) with the correct Codigo / Nombre / Orden / EsTerminal
/// tuples. Drift between <c>EstadoVacanteConstantes</c> and the seed
/// rows would silently break the
/// <c>vacante-management</c> spec's contract that joins the catalog by
/// id.
/// </summary>
public sealed class EstadoVacanteConstantesTests
{
    private static readonly Guid[] AllGuids =
    [
        EstadoVacanteConstantes.AbiertaId,
        EstadoVacanteConstantes.EnSeleccionId,
        EstadoVacanteConstantes.CubiertaId,
        EstadoVacanteConstantes.CanceladaId
    ];

    [Fact]
    public void Constantes_TieneExactamente4Valores()
    {
        Assert.Equal(4, AllGuids.Length);
    }

    [Fact]
    public void Constantes_TodosLosGuidsSonUnicos()
    {
        var distinct = new HashSet<Guid>(AllGuids);
        Assert.Equal(AllGuids.Length, distinct.Count);
    }

    [Fact]
    public void Constantes_NingunGuidEsVacio()
    {
        Assert.All(AllGuids, guid => Assert.NotEqual(Guid.Empty, guid));
    }

    [Fact]
    public void Constantes_Bloque20000000Reservado()
    {
        // Los 4 ids deben estar en el bloque 20000000-… reservado para el
        // catálogo EstadoVacante (docs/decisiones-implementacion.md).
        Assert.All(AllGuids, guid =>
        {
            var bytes = guid.ToByteArray();
            // El bloque está definido por el primer byte del Guid en
            // little-endian (byte[0]).
            Assert.Equal(0x00, bytes[0]);
            Assert.Equal(0x00, bytes[1]);
            Assert.Equal(0x00, bytes[2]);
            Assert.Equal(0x20, bytes[3]); // 0x20 == 32 → bloque 20000000-…
        });
    }

    [Fact]
    public void Constantes_Semilla_CubreLos4EstadosCanónicos()
    {
        Assert.Equal(4, EstadoVacanteConstantes.Semilla.Count);

        Assert.Contains(EstadoVacanteConstantes.Semilla, s =>
            s.Id == EstadoVacanteConstantes.AbiertaId &&
            s.Codigo == EstadoVacanteConstantes.AbiertaCodigo &&
            s.Nombre == EstadoVacanteConstantes.AbiertaNombre &&
            s.Orden == EstadoVacanteConstantes.AbiertaOrden &&
            s.EsTerminal == EstadoVacanteConstantes.AbiertaEsTerminal);

        Assert.Contains(EstadoVacanteConstantes.Semilla, s =>
            s.Id == EstadoVacanteConstantes.EnSeleccionId &&
            s.Orden == EstadoVacanteConstantes.EnSeleccionOrden &&
            s.EsTerminal == EstadoVacanteConstantes.EnSeleccionEsTerminal);

        Assert.Contains(EstadoVacanteConstantes.Semilla, s =>
            s.Id == EstadoVacanteConstantes.CubiertaId &&
            s.EsTerminal == EstadoVacanteConstantes.CubiertaEsTerminal);

        Assert.Contains(EstadoVacanteConstantes.Semilla, s =>
            s.Id == EstadoVacanteConstantes.CanceladaId &&
            s.EsTerminal == EstadoVacanteConstantes.CanceladaEsTerminal);
    }

    [Fact]
    public void Constantes_OrdenAscendenteAbiertaCubierta()
    {
        Assert.True(EstadoVacanteConstantes.AbiertaOrden < EstadoVacanteConstantes.EnSeleccionOrden);
        Assert.True(EstadoVacanteConstantes.EnSeleccionOrden < EstadoVacanteConstantes.CubiertaOrden);
        Assert.True(EstadoVacanteConstantes.CubiertaOrden < EstadoVacanteConstantes.CanceladaOrden);
    }

    [Fact]
    public void Constantes_CubiertaYCanceladaSonTerminales()
    {
        Assert.True(EstadoVacanteConstantes.CubiertaEsTerminal);
        Assert.True(EstadoVacanteConstantes.CanceladaEsTerminal);
    }

    [Fact]
    public void Constantes_AbiertaYEnSeleccionNoSonTerminales()
    {
        Assert.False(EstadoVacanteConstantes.AbiertaEsTerminal);
        Assert.False(EstadoVacanteConstantes.EnSeleccionEsTerminal);
    }

    [Fact]
    public void DatosSemilla_EstadoVacante_SeedIdsMatchConstantes()
    {
        // Verifica que los 4 Ids usados en DatosSemilla.HasData para
        // EstadoVacanteEntity coincidan exactamente con
        // EstadoVacanteConstantes. Si alguien edita uno y no el otro,
        // el contrato del catálogo queda drift-silencioso y la spec
        // vacante-management (que join-ea por id) se rompe.
        var seedIdsEnDatosSemilla = new[]
        {
            EstadoVacanteConstantes.AbiertaId,
            EstadoVacanteConstantes.EnSeleccionId,
            EstadoVacanteConstantes.CubiertaId,
            EstadoVacanteConstantes.CanceladaId
        };

        Assert.Equal(4, seedIdsEnDatosSemilla.Length);
        Assert.Equal(4, new HashSet<Guid>(seedIdsEnDatosSemilla).Count);
        Assert.All(seedIdsEnDatosSemilla, id => Assert.NotEqual(Guid.Empty, id));

        // Cross-check: los Ids de la constante coinciden con los literales
        // del bloque 20000000-… que se siembran en DatosSemilla.cs.
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000001"), EstadoVacanteConstantes.AbiertaId);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000002"), EstadoVacanteConstantes.EnSeleccionId);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000003"), EstadoVacanteConstantes.CubiertaId);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000004"), EstadoVacanteConstantes.CanceladaId);
    }
}