using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Tests.Web.Persona;
using SGV.Web.Pages.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

public sealed class PersonaHabilidadesPageTests
{
    [Fact]
    public void PageModel_RequiresAdministratorRole()
    {
        var authorize = Assert.Single(typeof(PersonaHabilidadesModel)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        Assert.Equal(RolesSgv.Administrador, ((AuthorizeAttribute)authorize).Roles);
    }

    [Fact]
    public async Task Get_Anonymous_DoesNotLoadPersonaData()
    {
        var apiClient = FakePersonaApiClient.WithPersonaList();
        var page = CreatePage(apiClient, authenticated: false);

        var result = await page.OnGetAsync(Guid.NewGuid());

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(apiClient.GetAllCalls);
        Assert.Empty(apiClient.GetSkillsCalls);
    }

    [Fact]
    public async Task Get_AuthenticatedWithoutAdministratorRole_IsForbidden()
    {
        var apiClient = FakePersonaApiClient.WithPersonaList();
        var page = CreatePage(apiClient, authenticated: true);

        var result = await page.OnGetAsync(Guid.NewGuid());

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(apiClient.GetAllCalls);
        Assert.Empty(apiClient.GetSkillsCalls);
    }

    [Fact]
    public async Task Get_Administrator_LoadsPersonaAndSkillsIntoViewModel()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var levelId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId,
            "L-001",
            "Ana",
            "García",
            "ana@example.com",
            null,
            "DNI",
            "Documento",
            "30123456",
            null,
            true);
        var skill = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var level = new NivelHabilidadDto(levelId, "AVZ", "Avanzado", 3, 3);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.GetSkillsResult = [new PersonaSkillDetailDto(skill, level)];
        var page = CreatePage(apiClient, authenticated: true, administrator: true);

        var result = await page.OnGetAsync(personaId);

        Assert.IsType<PageResult>(result);
        Assert.Equal(personaId, page.ViewModel.PersonaId);
        Assert.Equal("Ana García", page.ViewModel.PersonaNombre);
        var row = Assert.Single(page.ViewModel.Skills);
        Assert.Equal(skillId, row.SkillId);
        Assert.Equal("Liderazgo", row.SkillNombre);
        Assert.Equal(levelId, row.NivelHabilidadId);
        Assert.Equal("Avanzado", row.NivelNombre);
        Assert.Equal([personaId], apiClient.GetSkillsCalls);
    }

    [Fact]
    public async Task Get_InactivePersona_RedirectsToNotFoundWithoutLoadingSkills()
    {
        var personaId = Guid.NewGuid();
        var inactive = new PersonaDto(
            personaId,
            "L-002",
            "Persona",
            "Inactiva",
            null,
            null,
            null,
            null,
            null,
            null,
            false);
        var apiClient = FakePersonaApiClient.WithPersonaList(inactive);
        apiClient.GetSkillsResult = [new PersonaSkillDetailDto(
            new HabilidadDto(Guid.NewGuid(), "H-002", "Debe ignorarse", null, null),
            new NivelHabilidadDto(Guid.NewGuid(), "BAS", "Básico", 1, 1))];
        var page = CreatePage(apiClient, authenticated: true, administrator: true);

        var result = await page.OnGetAsync(personaId);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/error/404", redirect.Url);
        Assert.Empty(apiClient.GetSkillsCalls);
    }

    private static PersonaHabilidadesModel CreatePage(
        FakePersonaApiClient apiClient,
        bool authenticated,
        bool administrator = false)
    {
        var claims = authenticated
            ? new List<Claim> { new(ClaimTypes.Name, "test-user") }
            : [];
        if (administrator)
        {
            claims.Add(new Claim(ClaimTypes.Role, RolesSgv.Administrador));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null))
        };
        var pageContext = new PageContext(new ActionContext(
            httpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.RazorPages.PageActionDescriptor()))
        {
            ModelState = new ModelStateDictionary()
        };

        return new PersonaHabilidadesModel(apiClient, NullLogger<PersonaHabilidadesModel>.Instance)
        {
            PageContext = pageContext
        };
    }
}
