using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Ocupaciones;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Contrato público de <see cref="IOcupacionApiClient"/> para Slice 2.
/// La superficie crece en Slice 3a (Crear/Actualizar/Finalizar/Eliminar/Reactivar);
/// este test pin los 2 métodos de lectura vigentes para evitar regresiones
/// silenciosas en la firma.
/// </summary>
public sealed class IOcupacionApiClientContractTests
{
    [Fact]
    public void Interface_ExposesQueryAndGetByIdWithExpectedSignatures()
    {
        var interfaceType = typeof(IOcupacionApiClient);

        var listarMethod = Assert.Single(
            interfaceType.GetMethods(),
            m => m.Name == nameof(IOcupacionApiClient.ListarAsync));

        Assert.Equal(typeof(Task<PagedResult<OcupacionDto>>), listarMethod.ReturnType);
        Assert.Equal(typeof(OcupacionListQuery), listarMethod.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), listarMethod.GetParameters()[1].ParameterType);

        var obtenerMethod = Assert.Single(
            interfaceType.GetMethods(),
            m => m.Name == nameof(IOcupacionApiClient.ObtenerPorIdAsync));

        Assert.Equal(typeof(Task<OcupacionDto?>), obtenerMethod.ReturnType);
        Assert.Equal(typeof(Guid), obtenerMethod.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), obtenerMethod.GetParameters()[1].ParameterType);
    }

    [Fact]
    public void Interface_DoesNotExposeMutationMethodsYet_Slice3aAddsThem()
    {
        var interfaceType = typeof(IOcupacionApiClient);
        var mutationNames = new[]
        {
            "CrearAsync", "ActualizarAsync", "FinalizarAsync",
            "EliminarAsync", "ReactivarAsync"
        };

        foreach (var name in mutationNames)
        {
            Assert.True(
                interfaceType.GetMethod(name) is null,
                $"IOcupacionApiClient must not expose {name} in Slice 2; reserved for Slice 3a.");
        }
    }
}