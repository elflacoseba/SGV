using System.Linq;
using System.Reflection;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;
using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Contract-approval tests for <see cref="IPuestosApiClient"/>. Congelan las
/// firmas exactas que la Razor Page consume vía dependency injection: si
/// alguien borra un método, le cambia el nombre, devuelve un tipo distinto o
/// renombra un parámetro, el test falla ANTES de que el cambio silencioso
/// rompa la integración. Espejo de <c>ICargoApiClientContractTests</c>.
/// </summary>
public class IPuestosApiClientContractTests
{
    [Fact]
    public void Interface_ExposesGetAllAsyncWithExpectedSignature()
    {
        var method = typeof(IPuestosApiClient).GetMethod(nameof(IPuestosApiClient.GetAllAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IReadOnlyList<PuestoDto>>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("cancellationToken", parameters[0].Name);
        Assert.Equal(typeof(CancellationToken), parameters[0].ParameterType);
        Assert.True(parameters[0].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesGetByIdAsyncWithExpectedSignature()
    {
        var method = typeof(IPuestosApiClient).GetMethod(nameof(IPuestosApiClient.GetByIdAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PuestoDto?>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesCreateAsyncWithExpectedSignature()
    {
        var method = typeof(IPuestosApiClient).GetMethod(nameof(IPuestosApiClient.CreateAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PuestoCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(CrearPuestoRequest), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesUpdateAsyncWithExpectedSignature()
    {
        var method = typeof(IPuestosApiClient).GetMethod(nameof(IPuestosApiClient.UpdateAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PuestoCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("request", parameters[1].Name);
        Assert.Equal(typeof(ActualizarPuestoRequest), parameters[1].ParameterType);
        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.True(parameters[2].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesDeleteAsyncWithExpectedSignature()
    {
        var method = typeof(IPuestosApiClient).GetMethod(nameof(IPuestosApiClient.DeleteAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PuestoDeleteResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesQueryAsyncWithExpectedSignature()
    {
        var method = typeof(IPuestosApiClient).GetMethod(nameof(IPuestosApiClient.QueryAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PagedResult<PuestoDto>>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("query", parameters[0].Name);
        Assert.Equal(typeof(PuestoListQuery), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesReactivateAsyncWithExpectedSignature()
    {
        var method = typeof(IPuestosApiClient).GetMethod(nameof(IPuestosApiClient.ReactivateAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PuestoCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesExactlySevenPublicMethods()
    {
        // Defensa contra refactors que sumen métodos fuera del contrato
        // documentado en design.md §3.1. El módulo de Puestos NO tiene
        // subrecurso skills ni catálogo de niveles (a diferencia de Cargos),
        // por lo que la superficie pública son exactamente 6 métodos.
        var methodNames = typeof(IPuestosApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "CreateAsync", "DeleteAsync", "GetAllAsync", "GetByIdAsync", "QueryAsync", "ReactivateAsync", "UpdateAsync" },
            methodNames);
    }
}
