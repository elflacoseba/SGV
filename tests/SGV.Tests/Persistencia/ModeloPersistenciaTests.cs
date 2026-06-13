using Microsoft.EntityFrameworkCore;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Seleccion;
using SGV.Infraestructura.Persistencia;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class ModeloPersistenciaTests
{
    private readonly SgvDbContext _contexto = new SgvDbContextFactory().CreateDbContext([]);

    [Fact]
    public void Modelo_ConfiguraIndiceUnicoParaOcupacionVigentePorPuesto()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(Ocupacion));

        var indice = entidad!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(Ocupacion.PuestoId)]));

        Assert.True(indice.IsUnique);
        Assert.Contains("FechaFin", indice.GetFilter(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Modelo_ConfiguraCodigoUnicoFiltradoParaPuestosActivos()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(Puesto));

        var indice = entidad!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(Puesto.Codigo)]));

        Assert.True(indice.IsUnique);
        Assert.Contains("IsDeleted", indice.GetFilter(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Modelo_ConfiguraPostulacionUnicaPorVacanteYPostulante()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(Postulacion));

        var indice = entidad!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(Postulacion.VacanteId), nameof(Postulacion.PostulanteId)]));

        Assert.True(indice.IsUnique);
    }
}
