using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Comandos;
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
            ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), new ModelStateDictionary())
        };

        return new PersonaHabilidadesModel(apiClient, NullLogger<PersonaHabilidadesModel>.Instance)
        {
            PageContext = pageContext
        };
    }

    private static PersonaHabilidadesModel CreatePostPage(
        FakePersonaApiClient apiClient,
        bool administrator,
        IFormCollection form)
    {
        var page = CreatePage(apiClient, authenticated: true, administrator: administrator);
        page.PageContext.HttpContext.Request.Form = form;
        page.TempData = new TempDataDictionary(
            page.PageContext.HttpContext,
            new NoopTempDataProvider());
        return page;
    }

    private sealed class NoopTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private static FormCollection BuildAsignarForm(Guid? skillId, Guid? nivelId)
    {
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        if (skillId.HasValue) dict["SkillId"] = skillId.Value.ToString();
        if (nivelId.HasValue) dict["NivelHabilidadId"] = nivelId.Value.ToString();
        return new FormCollection(dict);
    }

    private static FormCollection BuildQuitarForm(Guid skillId)
        => new(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["SkillId"] = skillId.ToString()
        });

    // ──────────────────────────────────────────────
    // 3b.1 — RED: tests handlers POST upsert/delete con PRG
    // (cubre flujo PostAsignar → redirect → TempData success/warning/danger
    // y PostQuitar → redirect → TempData success/warning/danger)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostAsignar_Admin_Success_PerformsUpsertAndRedirectsViaPrg()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertResult = PersonaSkillCommandResult.Success(
            new PersonaSkillDto(skillId, nivelId));
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId, nivelId));

        var result = await page.OnPostAsignarAsync(personaId);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Equal(personaId, redirect.RouteValues!["id"]);
        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal((personaId, skillId, new AsignarPersonaSkillRequest(nivelId)), upsert);
    }

    [Fact]
    public async Task PostAsignar_Admin_Success_SetsSuccessTempDataMessage()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertResult = PersonaSkillCommandResult.Success(
            new PersonaSkillDto(skillId, nivelId));
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId, nivelId));

        await page.OnPostAsignarAsync(personaId);

        Assert.Equal("success", page.TempData["StatusKind"]);
        Assert.NotNull(page.TempData["StatusMessage"]);
        Assert.IsType<string>(page.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task PostAsignar_NonAdmin_ForbiddenWithoutInvokingClient()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var apiClient = FakePersonaApiClient.WithPersonaList();
        var page = CreatePostPage(apiClient, administrator: false,
            BuildAsignarForm(skillId, nivelId));

        var result = await page.OnPostAsignarAsync(personaId);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(apiClient.SkillUpsertCalls);
    }

    [Fact]
    public async Task PostAsignar_MissingSkillId_AddsModelStateErrorAndStaysOnPage()
    {
        var personaId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId: null, nivelId));

        var result = await page.OnPostAsignarAsync(personaId);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(apiClient.SkillUpsertCalls);
    }

    [Fact]
    public async Task PostAsignar_MissingNivelId_AddsModelStateErrorAndStaysOnPage()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId, nivelId: null));

        var result = await page.OnPostAsignarAsync(personaId);

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(apiClient.SkillUpsertCalls);
    }

    [Fact]
    public async Task PostAsignar_BackendValidationFailure_RedirectsWithDangerTempData()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertResult = PersonaSkillCommandResult.Failure(
            new PersonaSkillError(
                PersonaSkillErrorType.Validation,
                "NivelHabilidadNoExiste",
                "El nivel no existe.",
                StatusCode: 400,
                Categoria: ErrorCategoria.Validation));
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId, nivelId));

        var result = await page.OnPostAsignarAsync(personaId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("danger", page.TempData["StatusKind"]);
        Assert.NotNull(page.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task PostAsignar_BackendConflictFailure_RedirectsWithDangerTempData()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertResult = PersonaSkillCommandResult.Failure(
            new PersonaSkillError(
                PersonaSkillErrorType.Validation,
                "Conflict",
                "Conflicto al procesar la operación.",
                StatusCode: 409,
                Categoria: ErrorCategoria.Conflict));
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId, nivelId));

        var result = await page.OnPostAsignarAsync(personaId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("danger", page.TempData["StatusKind"]);
    }

    [Fact]
    public async Task PostAsignar_BackendNotFoundFailure_RedirectsWithDangerTempData()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertResult = PersonaSkillCommandResult.Failure(
            new PersonaSkillError(
                PersonaSkillErrorType.NotFound,
                "PersonaNoEncontrada",
                "La persona no existe.",
                StatusCode: 404,
                Categoria: ErrorCategoria.NotFound));
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId, nivelId));

        var result = await page.OnPostAsignarAsync(personaId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("danger", page.TempData["StatusKind"]);
    }

    [Fact]
    public async Task PostAsignar_TransportFailure_RedirectsWithDangerTempDataAndNoStackTrace()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertException = new HttpRequestException("network down");
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId, nivelId));

        var result = await page.OnPostAsignarAsync(personaId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("danger", page.TempData["StatusKind"]);
        var message = Assert.IsType<string>(page.TempData["StatusMessage"]);
        Assert.DoesNotContain("HttpRequestException", message);
        Assert.DoesNotContain("network down", message);
    }

    [Fact]
    public async Task PostQuitar_Admin_Success_CallsDeleteAndRedirectsViaPrg()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteResult = new PersonaSkillDeleteResult(
            true, HttpStatusCode.NoContent, null, null);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildQuitarForm(skillId));

        var result = await page.OnPostQuitarAsync(personaId, skillId);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(personaId, redirect.RouteValues!["id"]);
        var delete = Assert.Single(apiClient.SkillDeleteCalls);
        Assert.Equal((personaId, skillId), delete);
    }

    [Fact]
    public async Task PostQuitar_Admin_Success_SetsSuccessTempDataMessage()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteResult = new PersonaSkillDeleteResult(
            true, HttpStatusCode.NoContent, null, null);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildQuitarForm(skillId));

        await page.OnPostQuitarAsync(personaId, skillId);

        Assert.Equal("success", page.TempData["StatusKind"]);
        Assert.NotNull(page.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task PostQuitar_NonAdmin_ForbiddenWithoutInvokingClient()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var apiClient = FakePersonaApiClient.WithPersonaList();
        var page = CreatePostPage(apiClient, administrator: false,
            BuildQuitarForm(skillId));

        var result = await page.OnPostQuitarAsync(personaId, skillId);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(apiClient.SkillDeleteCalls);
    }

    [Fact]
    public async Task PostQuitar_BackendNotFound_RedirectsWithWarningTempData()
    {
        // 404 al quitar no es un error fatal: refleja una race condition
        // real (otra pestaña quitó la asociación). PRG con TempData
        // warning permite que el siguiente GET refresque la grilla sin
        // asustar al usuario con un modal de error.
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteResult = new PersonaSkillDeleteResult(
            false, HttpStatusCode.NotFound, "AsociacionNoEncontrada",
            "La asociación ya no existe.", Categoria: ErrorCategoria.NotFound);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildQuitarForm(skillId));

        var result = await page.OnPostQuitarAsync(personaId, skillId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("warning", page.TempData["StatusKind"]);
    }

    [Fact]
    public async Task PostQuitar_BackendConflict_RedirectsWithDangerTempData()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteResult = new PersonaSkillDeleteResult(
            false, HttpStatusCode.Conflict, "Conflict",
            "Conflicto.", Categoria: ErrorCategoria.Conflict);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildQuitarForm(skillId));

        var result = await page.OnPostQuitarAsync(personaId, skillId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("danger", page.TempData["StatusKind"]);
    }

    [Fact]
    public async Task PostQuitar_BackendValidation_RedirectsWithDangerTempData()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteResult = new PersonaSkillDeleteResult(
            false, HttpStatusCode.BadRequest, "DatosInvalidos",
            "Datos inválidos.", Categoria: ErrorCategoria.Validation);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildQuitarForm(skillId));

        var result = await page.OnPostQuitarAsync(personaId, skillId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("danger", page.TempData["StatusKind"]);
    }

    [Fact]
    public async Task PostQuitar_TransportFailure_RedirectsWithDangerTempData()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteException = new HttpRequestException("network down");
        var page = CreatePostPage(apiClient, administrator: true,
            BuildQuitarForm(skillId));

        var result = await page.OnPostQuitarAsync(personaId, skillId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("danger", page.TempData["StatusKind"]);
        var message = Assert.IsType<string>(page.TempData["StatusMessage"]);
        Assert.DoesNotContain("HttpRequestException", message);
        Assert.DoesNotContain("network down", message);
    }

    // ──────────────────────────────────────────────
    // 3b.2 — RED: tests POST persona inactiva bloquea mutación
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostAsignar_InactivePersona_RedirectsWithoutInvokingClient()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var inactive = new PersonaDto(
            personaId, "L-002", "Persona", "Inactiva",
            null, null, null, null, null, null, false);
        var apiClient = FakePersonaApiClient.WithPersonaList(inactive);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildAsignarForm(skillId, nivelId));

        var result = await page.OnPostAsignarAsync(personaId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(apiClient.SkillUpsertCalls);
    }

    [Fact]
    public async Task PostQuitar_InactivePersona_RedirectsWithoutInvokingClient()
    {
        var personaId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var inactive = new PersonaDto(
            personaId, "L-002", "Persona", "Inactiva",
            null, null, null, null, null, null, false);
        var apiClient = FakePersonaApiClient.WithPersonaList(inactive);
        var page = CreatePostPage(apiClient, administrator: true,
            BuildQuitarForm(skillId));

        var result = await page.OnPostQuitarAsync(personaId, skillId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(apiClient.SkillDeleteCalls);
    }
}
