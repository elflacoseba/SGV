using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class UsuariosControllerTests
{
    private readonly ApiIntegrationFixture _fixture;

    public UsuariosControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetUsuarios_WithAuthenticatedNonAdmin_ReturnsOk()
    {
        var client = _fixture.RootFactory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/usuarios");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<IReadOnlyList<UsuarioDto>>();
        var user = Assert.Single(users!);
        Assert.Equal("Juan", user.Nombres);
        Assert.Equal("Perez", user.Apellidos);
    }

    [Fact]
    public async Task GetUsuarios_WithoutCredentials_ReturnsUnauthorized()
    {
        var client = _fixture.RootFactory.CreateClient();

        var response = await client.GetAsync("/api/v1/usuarios");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConsulta_WithAuthenticatedNonAdmin_ReturnsPagedUsers()
    {
        var client = _fixture.RootFactory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/usuarios/consulta?page=1&pageSize=20&status=activas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<UsuarioDto>>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        var user = Assert.Single(result.Items);
        Assert.Equal([RolesSgv.Administrador], user.Roles);
    }

    [Fact]
    public async Task GetConsulta_InvalidStatusAndPagination_NormalizesToActivePageBounds()
    {
        var fake = new FakeUsuarioServicioConsulta();
        await using var factory = WithUsuarioConsulta(fake);
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/usuarios/consulta?status=archivo&page=0&pageSize=500");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(fake.LastQuery);
        Assert.Equal(UsuarioSegmentoListado.Activas, fake.LastQuery.Segmento);
        Assert.Equal(1, fake.LastQuery.Page);
        Assert.Equal(100, fake.LastQuery.PageSize);
    }

    [Fact]
    public async Task GetConsulta_SizeAlias_NormalizesAndForwardsSearchAndSort()
    {
        var fake = new FakeUsuarioServicioConsulta();
        await using var factory = WithUsuarioConsulta(fake);
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync(
            "/api/v1/usuarios/consulta?page=2&size=25&search=juan&sort=apellidos_desc&status=eliminadas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(fake.LastQuery);
        Assert.Equal(2, fake.LastQuery.Page);
        Assert.Equal(25, fake.LastQuery.PageSize);
        Assert.Equal("juan", fake.LastQuery.Search);
        Assert.Equal("apellidos_desc", fake.LastQuery.Sort);
        Assert.Equal(UsuarioSegmentoListado.Eliminadas, fake.LastQuery.Segmento);
    }

    [Fact]
    public async Task GetById_ExistingUser_ReturnsDto()
    {
        var client = _fixture.RootFactory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/usuarios/user-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.Equal("user-1", user!.Id);
        Assert.Equal("admin", user.UserName);
    }

    [Fact]
    public async Task GetById_MissingUser_ReturnsNotFound()
    {
        var client = _fixture.RootFactory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/usuarios/missing-user");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithAdminCredentials_ReturnsFixedCatalog()
    {
        var client = _fixture.RootFactory.CreateAdminClient();

        var roles = await client.GetFromJsonAsync<IReadOnlyList<string>>("/api/v1/usuarios/roles");

        Assert.Equal(RolesSgv.Todos, roles);
    }

    [Fact]
    public async Task GetRoles_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var client = _fixture.RootFactory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/usuarios/roles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithAdmin_ReturnsCreatedAtGetById()
    {
        var client = _fixture.RootFactory.CreateAdminClient();
        var request = new CrearUsuarioRequest(
            FakePersonaServicioConsulta.PersonaId1,
            "created",
            "created@test.com",
            "Password1!",
            [RolesSgv.Consultor]);

        var response = await client.PostAsJsonAsync("/api/v1/usuarios", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("http://localhost/api/v1/usuarios/user-1", response.Headers.Location?.ToString());
        var user = await response.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.Equal("created", user!.UserName);
    }

    [Fact]
    public async Task Put_WithAdmin_UpdatesUserNameEmailAndRolesInOneRequest()
    {
        var client = _fixture.RootFactory.CreateAdminClient();
        var request = new ActualizarUsuarioRequest(
            "renamed",
            "renamed@test.com",
            [RolesSgv.GestorVacantes]);

        var response = await client.PutAsJsonAsync("/api/v1/usuarios/user-1", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.Equal("renamed", user!.UserName);
        Assert.Equal("renamed@test.com", user.Email);
        Assert.Equal([RolesSgv.GestorVacantes], user.Roles);
    }

    [Fact]
    public async Task Put_DuplicateUserName_ReturnsConflictWithDomainCode()
    {
        var fake = new FakeUsuarioServicioComandos
        {
            ActualizarHandler = (_, _, _) => Task.FromResult(UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.Conflict,
                "UserNameDuplicado",
                "El nombre de usuario ya está en uso.",
                Categoria: ErrorCategoria.Conflict)))
        };
        await using var factory = WithUsuarioComandos(fake);
        var client = factory.CreateAdminClient();

        var response = await client.PutAsJsonAsync(
            "/api/v1/usuarios/user-1",
            new ActualizarUsuarioRequest("duplicate", "user@test.com", [RolesSgv.Consultor]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("UserNameDuplicado", problem!.Title);
        Assert.Equal(409, problem.Status);
    }

    [Fact]
    public async Task Delete_CurrentUser_ReturnsForbiddenAutoBaja()
    {
        var fake = new FakeUsuarioServicioComandos
        {
            DesactivarHandler = (_, _) => Task.FromResult(UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.Unauthorized,
                "AutoBaja",
                "No puede desactivar su propio usuario.",
                Categoria: ErrorCategoria.Forbidden)))
        };
        await using var factory = WithUsuarioComandos(fake);
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync("/api/v1/usuarios/user-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("AutoBaja", problem!.Title);
        Assert.Equal(403, problem.Status);
    }

    [Fact]
    public async Task Delete_OtherUser_ReturnsNoContent()
    {
        var client = _fixture.RootFactory.CreateAdminClient();

        var response = await client.DeleteAsync("/api/v1/usuarios/user-2");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task Reactivate_WithInactivePersona_ReturnsConflictPersonaInactiva()
    {
        var fake = new FakeUsuarioServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.Conflict,
                "PersonaInactiva",
                "La persona asociada debe reactivarse primero.",
                Categoria: ErrorCategoria.Conflict)))
        };
        await using var factory = WithUsuarioComandos(fake);
        var client = factory.CreateAdminClient();

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/usuarios/user-1/reactivar");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("PersonaInactiva", problem!.Title);
        Assert.Equal(409, problem.Status);
    }

    [Fact]
    public async Task Reactivate_WithAdmin_ReturnsUpdatedDto()
    {
        var client = _fixture.RootFactory.CreateAdminClient();

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/usuarios/user-1/reactivar");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.Equal("user-1", user!.Id);
    }

    [Theory]
    [InlineData(nameof(SGV.Api.Controllers.UsuariosController.Create))]
    [InlineData(nameof(SGV.Api.Controllers.UsuariosController.AssignRoles))]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Reactivate")]
    public void MutationAction_RequiresAdministratorRole(string methodName)
    {
        var method = typeof(SGV.Api.Controllers.UsuariosController)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var authorize = Assert.Single(method!.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(RolesSgv.Administrador, authorize.Roles);
    }

    [Theory]
    [InlineData("POST", "/api/v1/usuarios")]
    [InlineData("PUT", "/api/v1/usuarios/user-1")]
    [InlineData("PUT", "/api/v1/usuarios/user-1/roles")]
    [InlineData("DELETE", "/api/v1/usuarios/user-1")]
    [InlineData("PATCH", "/api/v1/usuarios/user-1/reactivar")]
    public async Task Mutation_WithAuthenticatedNonAdmin_ReturnsForbidden(string method, string uri)
    {
        var client = _fixture.RootFactory.CreateNonAdminClient();
        var request = new HttpRequestMessage(new HttpMethod(method), uri);
        if (method is "POST")
        {
            request.Content = JsonContent.Create(new CrearUsuarioRequest(
                FakePersonaServicioConsulta.PersonaId1,
                "new-user",
                "new@test.com",
                "Password1!",
                [RolesSgv.Consultor]));
        }
        else if (method is "PUT" && !uri.EndsWith("/roles", StringComparison.Ordinal))
        {
            request.Content = JsonContent.Create(new ActualizarUsuarioRequest(
                "renamed",
                "renamed@test.com",
                [RolesSgv.Consultor]));
        }
        else if (method is "PUT")
        {
            request.Content = JsonContent.Create(new AsignarRolesRequest([RolesSgv.Consultor]));
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private ApiWebApplicationFactory WithUsuarioConsulta(FakeUsuarioServicioConsulta fake)
        => _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IUsuarioServicioConsulta>();
            services.AddSingleton<IUsuarioServicioConsulta>(fake);
        });

    private ApiWebApplicationFactory WithUsuarioComandos(FakeUsuarioServicioComandos fake)
        => _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IUsuarioServicioComandos>();
            services.AddSingleton<IUsuarioServicioComandos>(fake);
        });
}
