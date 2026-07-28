using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Ocupaciones;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Contrato público de <see cref="IOcupacionApiClient"/> tras Slice 3a
/// (change <c>2026-07-28-web-ocupaciones-issue-208</c>). Pinea la firma de
/// los 7 métodos que expone la interfaz (2 de lectura + 5 de mutación)
/// para evitar regresiones silenciosas en la superficie wire.
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
    public void Interface_ExposesFiveMutationMethods_Slice3aAddedThem()
    {
        var interfaceType = typeof(IOcupacionApiClient);

        Assert.NotNull(interfaceType.GetMethod(nameof(IOcupacionApiClient.CrearAsync)));
        Assert.NotNull(interfaceType.GetMethod(nameof(IOcupacionApiClient.ActualizarAsync)));
        Assert.NotNull(interfaceType.GetMethod(nameof(IOcupacionApiClient.FinalizarAsync)));
        Assert.NotNull(interfaceType.GetMethod(nameof(IOcupacionApiClient.EliminarAsync)));
        Assert.NotNull(interfaceType.GetMethod(nameof(IOcupacionApiClient.ReactivarAsync)));
    }

    [Fact]
    public void Interface_MutationMethodsHaveExpectedSignatures()
    {
        var interfaceType = typeof(IOcupacionApiClient);

        // CrearAsync(CrearOcupacionRequest, CancellationToken) → Task<OcupacionCommandResult>
        var crear = interfaceType.GetMethod(nameof(IOcupacionApiClient.CrearAsync))!;
        Assert.Equal(typeof(Task<OcupacionCommandResult>), crear.ReturnType);
        Assert.Equal(typeof(CrearOcupacionRequest), crear.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), crear.GetParameters()[1].ParameterType);

        // ActualizarAsync(Guid, ActualizarOcupacionRequest, CancellationToken) → Task<OcupacionCommandResult>
        var actualizar = interfaceType.GetMethod(nameof(IOcupacionApiClient.ActualizarAsync))!;
        Assert.Equal(typeof(Task<OcupacionCommandResult>), actualizar.ReturnType);
        Assert.Equal(typeof(Guid), actualizar.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(ActualizarOcupacionRequest), actualizar.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), actualizar.GetParameters()[2].ParameterType);

        // FinalizarAsync(Guid, FinalizarOcupacionRequest, CancellationToken) → Task<OcupacionCommandResult>
        var finalizar = interfaceType.GetMethod(nameof(IOcupacionApiClient.FinalizarAsync))!;
        Assert.Equal(typeof(Task<OcupacionCommandResult>), finalizar.ReturnType);
        Assert.Equal(typeof(Guid), finalizar.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(FinalizarOcupacionRequest), finalizar.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), finalizar.GetParameters()[2].ParameterType);

        // EliminarAsync(Guid, CancellationToken) → Task<OcupacionCommandResult>
        var eliminar = interfaceType.GetMethod(nameof(IOcupacionApiClient.EliminarAsync))!;
        Assert.Equal(typeof(Task<OcupacionCommandResult>), eliminar.ReturnType);
        Assert.Equal(typeof(Guid), eliminar.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), eliminar.GetParameters()[1].ParameterType);

        // ReactivarAsync(Guid, CancellationToken) → Task<OcupacionCommandResult>
        var reactivar = interfaceType.GetMethod(nameof(IOcupacionApiClient.ReactivarAsync))!;
        Assert.Equal(typeof(Task<OcupacionCommandResult>), reactivar.ReturnType);
        Assert.Equal(typeof(Guid), reactivar.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), reactivar.GetParameters()[1].ParameterType);
    }
}