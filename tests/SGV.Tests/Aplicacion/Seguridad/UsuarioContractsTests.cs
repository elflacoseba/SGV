using System.Reflection;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Aplicacion.Seguridad;

public sealed class UsuarioContractsTests
{
    [Fact]
    public void UsuarioDto_AppendsNullablePersonaNamesAfterExistingProperties()
    {
        var constructor = Assert.Single(typeof(UsuarioDto).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Equal(
            ["Id", "PersonaId", "UserName", "Email", "Roles", "Nombres", "Apellidos"],
            parameters.Select(parameter => parameter.Name!).ToArray());
        Assert.Equal(typeof(string), parameters[^2].ParameterType);
        Assert.Equal(typeof(string), parameters[^1].ParameterType);
        Assert.True(new NullabilityInfoContext().Create(parameters[^2]).ReadState is NullabilityState.Nullable);
        Assert.True(new NullabilityInfoContext().Create(parameters[^1]).ReadState is NullabilityState.Nullable);
    }

    [Fact]
    public void UsuarioListQuery_DefaultsToActiveSegment()
    {
        var query = new UsuarioListQuery(2, 25, "ana", "apellidos_desc");

        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal("ana", query.Search);
        Assert.Equal("apellidos_desc", query.Sort);
        Assert.Equal(UsuarioSegmentoListado.Activas, query.Segmento);
    }

    [Fact]
    public void UsuarioListadoDto_WrapsPagedResultWithoutChangingPaginationMetadata()
    {
        var page = new PagedResult<UsuarioDto>(
            [new UsuarioDto("user-1", Guid.NewGuid(), "admin", "admin@test.com", ["Administrador"], "Ana", "Pérez")],
            31,
            2,
            20);

        var result = new UsuarioListadoDto(page);

        Assert.Same(page, result.Result);
        Assert.Equal(31, result.Result.TotalCount);
        Assert.Equal(2, result.Result.Page);
        Assert.Equal(20, result.Result.PageSize);
    }

    [Fact]
    public void ActualizarUsuarioRequest_CarriesCredentialsAndRoleSetAtomically()
    {
        var request = new ActualizarUsuarioRequest("new-name", "new@test.com", ["Consultor"]);

        Assert.Equal("new-name", request.UserName);
        Assert.Equal("new@test.com", request.Email);
        Assert.Equal(["Consultor"], request.Roles);
    }
}
