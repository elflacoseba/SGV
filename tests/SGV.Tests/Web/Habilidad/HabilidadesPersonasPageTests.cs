using System.Net;
using System.Web;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests de la Razor Page readonly <c>Pages/Organizacion/Habilidades/Personas.cshtml</c>
/// introducida por el change <c>agrega-navegacion-personas-habilidades</c>
/// (PR C — frontend subreverso). Cubren los escenarios del design §6:
///   - Carga inicial con habilidad existente y personas.
///   - Habilidad inexistente → estado recuperable.
///   - Toggle activas/eliminadas y propagación de search/sort/status.
///   - Guid.Empty → estado recuperable sin 500.
///   - Enlace al detalle de Persona (REQ-HM-NEW-LINK).
///   - Acceso anónimo redirige a sign-in (REQ-HM-NEW-AUTH).
/// </summary>
[Collection("WebIntegration")]
public sealed class HabilidadesPersonasPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public HabilidadesPersonasPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_PersonasPage_Anonymous_RedirectsToSignIn()
    {
        // REQ-HM-NEW-AUTH: la página usa [Authorize] sin restricción de rol;
        // un usuario anónimo debe ser redirigido al sign-in.
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/{Guid.NewGuid()}/personas");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_PersonasPage_ExistingSkillWithPersonas_RendersTableWithItems()
    {
        // REQ-HM-NEW-PAGE: la página muestra legajo, apellidos, nombres,
        // email y nivel de cada persona asociada.
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var nivelId = Guid.NewGuid();
        var nivel = new NivelHabilidadDto(nivelId, "AVZ", "Avanzado", 3, 3);
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId,
            Legajo: "L-100",
            Nombres: "Juan",
            Apellidos: "Pérez",
            Email: "juan@test",
            TipoDocumentoId: null,
            TipoDocumentoCodigo: null,
            TipoDocumentoNombre: null,
            NumeroDocumento: "12345678",
            Telefono: null,
            IsActive: true);

        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        apiClient.GetPersonasSeed(skillId, new[] { persona }, nivel);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Personas asociadas a la habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Liderazgo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-100", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pérez", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Juan", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("juan@test", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avanzado", content, StringComparison.OrdinalIgnoreCase);

        // El subrecurso fue invocado exactamente una vez con defaults normalizados.
        var call = Assert.Single(apiClient.GetPersonasCalls);
        Assert.Equal(skillId, call.SkillId);
        Assert.Equal(PersonaSegmentoListado.Activas, call.Query.Segmento);
        Assert.Equal(1, call.Query.Page);
        Assert.Equal(20, call.Query.PageSize);
    }

    [Fact]
    public async Task Get_PersonasPage_NonExistingSkill_RendersRecoverableState()
    {
        // GetByIdAsync devuelve null → la página entra en estado recuperable.
        var skillId = Guid.NewGuid();
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.GetByIdHandler = _ => null;

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Personas asociadas a la habilidad", content, StringComparison.OrdinalIgnoreCase);

        // El subrecurso GetPersonasAsync NO debe invocarse cuando la habilidad padre no existe.
        Assert.Empty(apiClient.GetPersonasCalls);
    }

    [Fact]
    public async Task Get_PersonasPage_EmptyGuid_RendersRecoverableStateWithoutServerError()
    {
        // Guid.Empty no debe romper la página ni producir 500. Convención
        // vigente en Cargos.cshtml: estado recuperable con mensaje específico.
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        // Forzamos un Guid.Empty en el path. El route constraint {id:guid}
        // matchea Guid.Empty como Guid válido, así que entra al handler.
        var response = await client.GetAsync($"/organizacion/habilidades/{Guid.Empty}/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Guid.Empty", content, StringComparison.OrdinalIgnoreCase);

        // Sin invocación del subrecurso: Guid.Empty fail-fast.
        Assert.Empty(apiClient.GetPersonasCalls);
    }

    [Fact]
    public async Task Get_PersonasPage_StatusEliminadas_PassesEliminadasSegment()
    {
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);

        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        apiClient.SeedPersonasEliminadas(skillId, new[]
        {
            new PersonaDto(
                Guid.NewGuid(),
                Legajo: "L-ELI",
                Nombres: "Ana",
                Apellidos: "López",
                Email: null,
                TipoDocumentoId: null,
                TipoDocumentoCodigo: null,
                TipoDocumentoNombre: null,
                NumeroDocumento: null,
                Telefono: null,
                IsActive: false)
        }, nivel);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/personas?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Personas eliminadas de la habilidad", content, StringComparison.OrdinalIgnoreCase);

        var call = Assert.Single(apiClient.GetPersonasCalls);
        Assert.Equal(PersonaSegmentoListado.Eliminadas, call.Query.Segmento);
    }

    [Fact]
    public async Task Get_PersonasPage_PaginationAndSearch_PreservedInSubresourceCall()
    {
        // El PageModel normaliza page/pageSize y propaga search/sort al
        // subrecurso. La página destino debe invocar GetPersonasAsync con
        // exactamente los valores normalizados.
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync(
            $"/organizacion/habilidades/{skillId}/personas?p=2&pageSize=5&search=gar&sort=apellidos_asc&status=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var call = Assert.Single(apiClient.GetPersonasCalls);
        Assert.Equal(2, call.Query.Page);
        Assert.Equal(5, call.Query.PageSize);
        Assert.Equal("gar", call.Query.Search);
        Assert.Equal("apellidos_asc", call.Query.Sort);
        Assert.Equal(PersonaSegmentoListado.Activas, call.Query.Segmento);
    }

    [Fact]
    public async Task Get_PersonasPage_RowLinksToPersonaDetails()
    {
        // REQ-HM-NEW-LINK: cada fila de la grilla debe enlazar al detalle
        // correspondiente en Pages/Personas/Details usando el identificador
        // de la persona.
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId,
            Legajo: "L-001",
            Nombres: "Juan",
            Apellidos: "Pérez",
            Email: null,
            TipoDocumentoId: null,
            TipoDocumentoCodigo: null,
            TipoDocumentoNombre: null,
            NumeroDocumento: null,
            Telefono: null,
            IsActive: true);

        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        apiClient.GetPersonasSeed(skillId, new[] { persona }, nivel);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_PersonasPage_EmptyResult_RendersEmptyState()
    {
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        // Sin seed → resultado vacío por defecto.

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Personas asociadas a la habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No hay personas asociadas", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_PersonasPage_TransportFailure_RendersRecoverableMessage()
    {
        // 5xx / falla de transporte en GetByIdAsync debe traducirse a un
        // estado recuperable con mensaje accionable (sin stack trace).
        var skillId = Guid.NewGuid();
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.GetByIdException = new HttpRequestException("network down");

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Intentá nuevamente", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestException", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", content, StringComparison.OrdinalIgnoreCase);
    }
}