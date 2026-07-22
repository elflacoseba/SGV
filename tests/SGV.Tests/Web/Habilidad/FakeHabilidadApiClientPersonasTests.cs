using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Unit tests para el seed determinista de
/// <see cref="FakeHabilidadApiClient.GetPersonasAsync"/>. Cubre los
/// escenarios del task C.3 del PR C (frontend subreverso):
///   - Skill con seed devuelve las personas sembradas.
///   - El parámetro <c>Search</c> filtra por legajo/apellido/nombres.
///   - Skill sin seed devuelve una página vacía.
///
/// PR agrega-navegacion-personas-habilidades / PR C — frontend subreverso
/// (task C.3 / C.4). Coverage spec: REQ-HM-NEW-PAGE (la página
/// Habilidades/Personas depende del seed determinista del fake para los
/// tests del PageModel).
/// </summary>
public sealed class FakeHabilidadApiClientPersonasTests
{
    [Fact]
    public async Task GetPersonasAsync_WithSeededSkill_ReturnsSeededPersonas()
    {
        // Sembramos 3 personas con la habilidad y verificamos que el fake
        // las devuelve respetando el segmento Activas (default) y la
        // paginación por defecto (page=1, pageSize=20).
        var skillId = Guid.NewGuid();
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var personas = new[]
        {
            BuildPersona(Guid.NewGuid(), "L-001", "Juan", "Pérez"),
            BuildPersona(Guid.NewGuid(), "L-002", "María", "García"),
            BuildPersona(Guid.NewGuid(), "L-003", "Lucía", "Suárez"),
        };

        var fake = FakeHabilidadApiClient.WithHabilidadList();
        fake.GetPersonasSeed(skillId, personas, nivel);

        var result = await fake.GetPersonasAsync(
            skillId,
            new HabilidadPersonasListQuery(1, 20, null, null, PersonaSegmentoListado.Activas));

        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, dto => Assert.Equal(skillId, dto.HabilidadId));
        // El segmento Activas es el default del fake.
        Assert.Equal(PersonaSegmentoListado.Activas, result.Segmento);

        // El fake registra la llamada con el query normalizado.
        var call = Assert.Single(fake.GetPersonasCalls);
        Assert.Equal(skillId, call.SkillId);
        Assert.Equal(1, call.Query.Page);
        Assert.Equal(20, call.Query.PageSize);
        Assert.Equal(PersonaSegmentoListado.Activas, call.Query.Segmento);
    }

    [Fact]
    public async Task GetPersonasAsync_WithSearch_ReturnsMatchingSeeded()
    {
        // Sembramos 3 personas; el filtro "gar" debe quedarse con María
        // García (match por apellido). Cobertura de REQ-SPQC-02 (búsqueda
        // sobre legajo, apellidos, nombres).
        var skillId = Guid.NewGuid();
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var personas = new[]
        {
            BuildPersona(Guid.NewGuid(), "L-001", "Juan", "Pérez"),
            BuildPersona(Guid.NewGuid(), "L-002", "María", "García"),
            BuildPersona(Guid.NewGuid(), "L-003", "Lucía", "Suárez"),
        };

        var fake = FakeHabilidadApiClient.WithHabilidadList();
        fake.GetPersonasSeed(skillId, personas, nivel);

        var result = await fake.GetPersonasAsync(
            skillId,
            new HabilidadPersonasListQuery(
                Page: 1,
                PageSize: 20,
                Search: "gar",
                Sort: null,
                Segmento: PersonaSegmentoListado.Activas));

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("García", result.Items[0].Persona.Apellidos);
    }

    [Fact]
    public async Task GetPersonasAsync_WithNonSeededSkill_ReturnsEmpty()
    {
        // Sin seed para el skillId consultado, el fake debe devolver una
        // página vacía con Segmento Activas. Paridad con
        // FakeHabilidadApiClient.GetCargosAsync (sin seed → empty page).
        var skillIdSolicitado = Guid.NewGuid();
        var otroSkill = Guid.NewGuid();
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);

        var fake = FakeHabilidadApiClient.WithHabilidadList();
        fake.GetPersonasSeed(otroSkill, new[] { BuildPersona(Guid.NewGuid(), "L-X", "Ana", "López") }, nivel);

        var result = await fake.GetPersonasAsync(
            skillIdSolicitado,
            new HabilidadPersonasListQuery(1, 20, null, null, PersonaSegmentoListado.Activas));

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
        Assert.Equal(PersonaSegmentoListado.Activas, result.Segmento);
    }

    [Fact]
    public async Task GetPersonasAsync_WithStatusEliminadas_ReturnsEmptyForActiveSeed()
    {
        // El segmento "eliminadas" se modela con un set paralelo de seeds
        // soft-deleted (paridad con FakeHabilidadApiClient.QueryAsync que
        // distingue activas vs eliminadas). Sin seeds en el segmento
        // Eliminadas, el resultado es vacío aunque el skill tenga activas.
        var skillId = Guid.NewGuid();
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);

        var fake = FakeHabilidadApiClient.WithHabilidadList();
        fake.GetPersonasSeed(skillId, new[] { BuildPersona(Guid.NewGuid(), "L-001", "Juan", "Pérez") }, nivel);

        var result = await fake.GetPersonasAsync(
            skillId,
            new HabilidadPersonasListQuery(1, 20, null, null, PersonaSegmentoListado.Eliminadas));

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    private static PersonaDto BuildPersona(Guid id, string legajo, string nombres, string apellidos) =>
        new(
            id,
            Legajo: legajo,
            Nombres: nombres,
            Apellidos: apellidos,
            Email: null,
            TipoDocumentoId: null,
            TipoDocumentoCodigo: null,
            TipoDocumentoNombre: null,
            NumeroDocumento: null,
            Telefono: null,
            IsActive: true);
}