using SGV.Aplicacion.Ocupaciones;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Dominio.Ocupaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Ocupaciones;

public sealed class OcupacionTipoAsignacionMapperTests
{
    [Theory]
    [InlineData(OcupacionTipoAsignacion.Permanente, TipoAsignacion.Permanente)]
    [InlineData(OcupacionTipoAsignacion.Interina, TipoAsignacion.Interina)]
    [InlineData(OcupacionTipoAsignacion.Temporal, TipoAsignacion.Temporal)]
    public void ToDomain_MapsContractToDomainByName(OcupacionTipoAsignacion contract, TipoAsignacion expected)
    {
        Assert.Equal(expected, OcupacionTipoAsignacionMapper.ToDomain(contract));
    }

    [Theory]
    [InlineData(TipoAsignacion.Permanente, OcupacionTipoAsignacion.Permanente)]
    [InlineData(TipoAsignacion.Interina, OcupacionTipoAsignacion.Interina)]
    [InlineData(TipoAsignacion.Temporal, OcupacionTipoAsignacion.Temporal)]
    public void ToContract_MapsDomainToContractByName(TipoAsignacion domain, OcupacionTipoAsignacion expected)
    {
        Assert.Equal(expected, OcupacionTipoAsignacionMapper.ToContract(domain));
    }

    [Fact]
    public void ToDomain_UnknownValue_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OcupacionTipoAsignacionMapper.ToDomain((OcupacionTipoAsignacion)99));
    }

    [Fact]
    public void ToContract_UnknownValue_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OcupacionTipoAsignacionMapper.ToContract((TipoAsignacion)99));
    }
}
