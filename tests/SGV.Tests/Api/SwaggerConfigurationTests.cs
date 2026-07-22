using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class SwaggerConfigurationTests
{
    private readonly ApiIntegrationFixture _fixture;
    private readonly WebApplicationFactory<SGV.Api.Program> _factory;

    public SwaggerConfigurationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
        _factory = fixture.RootFactory;
    }

    [Fact]
    public async Task SwaggerJsonEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerUiPage_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocument_HasInfoSection()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"SGV API\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"v1\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"paths\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwaggerDocument_DefinesBearerSchemeWithGlobalSecurityRequirement()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var securitySchemes = root.GetProperty("components").GetProperty("securitySchemes");

        Assert.True(securitySchemes.TryGetProperty("Bearer", out var bearer));
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.True(root.TryGetProperty("security", out var security));
        var securityArray = security.EnumerateArray();
        Assert.Single(securityArray);
        Assert.DoesNotContain("oauth2", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oauth2", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnonymousClient_CanStillReadSwaggerMetadataCollection()
    {
        // PR-1 (change 2026-07-09-agregar-autorizacion-api-restantes): antes
        // este test verificaba que un cliente anónimo podía leer
        // GET /api/v1/personas (200 OK). PR-1 cierra esa superficie con
        // [Authorize] a nivel de clase en PersonasController, por lo que el
        // body original ya no refleja la realidad del repo.
        //
        // Pivotamos el test al ÚNICO surface informativo que sigue siendo
        // público por diseño: el documento OpenAPI en
        // /swagger/v1/swagger.json. La aserción verifica explícitamente que
        // el cliente anónimo sigue pudiendo consumir swagger.json — un
        // invariant de discoverability que cualquier endurecimiento futuro
        // de auth debe preservar.
        //
        // NO reintroducimos un GET anónimo contra /api/v1/personas para
        // "honrar el nombre viejo": PR-1 quiere exactamente lo contrario
        // (que ese endpoint requiera token).
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocument_ListsAllResourcePaths()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        var actualPaths = new HashSet<string>();
        foreach (var path in paths.EnumerateObject())
        {
            actualPaths.Add(path.Name);
        }

        Assert.Contains("/api/v1/unidades-organizativas", actualPaths);
        Assert.Contains("/api/v1/unidades-organizativas/{id}/reactivar", actualPaths);
        Assert.Contains("/api/v1/cargos", actualPaths);
        Assert.Contains("/api/v1/cargos/{id}", actualPaths);
        Assert.Contains("/api/v1/cargos/{id}/reactivar", actualPaths);
        Assert.Contains("/api/v1/niveles-cargo", actualPaths);
        Assert.Contains("/api/v1/niveles-cargo/{id}", actualPaths);
        Assert.Contains("/api/v1/puestos", actualPaths);
        Assert.Contains("/api/v1/skills", actualPaths);
        Assert.Contains("/api/v1/skills/{id}", actualPaths);
        Assert.Contains("/api/v1/skills/{id}/reactivar", actualPaths);
        Assert.Contains("/api/v1/tipos-unidad-organizativa", actualPaths);
        Assert.Contains("/api/v1/personas", actualPaths);
        Assert.Contains("/api/v1/personas/{id}", actualPaths);
        Assert.Contains("/api/v1/personas/{id}/reactivar", actualPaths);
        Assert.Contains("/api/v1/ocupaciones", actualPaths);
        Assert.Contains("/api/v1/ocupaciones/{id}", actualPaths);
        Assert.Contains("/api/v1/ocupaciones/{id}/finalizar", actualPaths);
        Assert.Contains("/api/v1/ocupaciones/{id}/reactivar", actualPaths);
    }

    [Fact]
    public async Task UnidadesOrganizativas_ReactivarEndpoint_Documents200ResponseWithDto()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var reactivarPath = paths.GetProperty("/api/v1/unidades-organizativas/{id}/reactivar");
        var patchOp = reactivarPath.GetProperty("patch");
        var responses = patchOp.GetProperty("responses");

        // 200 response must exist with application/json content referencing UnidadOrganizativaDto
        var okResponse = responses.GetProperty("200");
        var okContent = okResponse.GetProperty("content");
        var jsonContent = okContent.GetProperty("application/json");
        var schema = jsonContent.GetProperty("schema");
        Assert.Equal("#/components/schemas/UnidadOrganizativaDto", schema.GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task UnidadesOrganizativas_ReactivarEndpoint_Documents404Response()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var reactivarPath = paths.GetProperty("/api/v1/unidades-organizativas/{id}/reactivar");
        var patchOp = reactivarPath.GetProperty("patch");
        var responses = patchOp.GetProperty("responses");

        Assert.True(responses.TryGetProperty("404", out _));
    }

    [Fact]
    public async Task UnidadesOrganizativas_ReactivarEndpoint_Documents409Response()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var reactivarPath = paths.GetProperty("/api/v1/unidades-organizativas/{id}/reactivar");
        var patchOp = reactivarPath.GetProperty("patch");
        var responses = patchOp.GetProperty("responses");

        Assert.True(responses.TryGetProperty("409", out _));
    }

    [Fact]
    public async Task NonOrgResources_OnlyExposeGetOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        foreach (var path in paths.EnumerateObject())
        {
            // Skip resources that intentionally expose write operations
            if (path.Name.StartsWith("/api/v1/unidades-organizativas", StringComparison.OrdinalIgnoreCase))
                continue;
            if (path.Name.StartsWith("/api/v1/cargos", StringComparison.OrdinalIgnoreCase))
                continue;
            if (path.Name.StartsWith("/api/v1/puestos", StringComparison.OrdinalIgnoreCase))
                continue;
            if (path.Name.StartsWith("/api/v1/skills", StringComparison.OrdinalIgnoreCase))
                continue;
            if (path.Name.StartsWith("/api/v1/personas", StringComparison.OrdinalIgnoreCase))
                continue;
            if (path.Name.StartsWith("/api/v1/usuarios", StringComparison.OrdinalIgnoreCase))
                continue;
            if (path.Name.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
                continue;
            if (path.Name.StartsWith("/api/v1/ocupaciones", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var operation in path.Value.EnumerateObject())
            {
                Assert.Equal("get", operation.Name, ignoreCase: true);
            }
        }
    }

    [Fact]
    public async Task Cargos_ExposesWriteOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Check collection path exposes POST and GET
        var collectionPath = paths.GetProperty("/api/v1/cargos");
        var collectionOps = new HashSet<string>();
        foreach (var op in collectionPath.EnumerateObject())
            collectionOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("post", collectionOps);
        Assert.Contains("get", collectionOps);

        // Check item path exposes GET, PUT, DELETE
        var itemPath = paths.GetProperty("/api/v1/cargos/{id}");
        var itemOps = new HashSet<string>();
        foreach (var op in itemPath.EnumerateObject())
            itemOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("get", itemOps);
        Assert.Contains("put", itemOps);
        Assert.Contains("delete", itemOps);

        // Check reactivar path exposes PATCH
        var reactivarPath = paths.GetProperty("/api/v1/cargos/{id}/reactivar");
        var reactivarOps = new HashSet<string>();
        foreach (var op in reactivarPath.EnumerateObject())
            reactivarOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", reactivarOps);
    }

    [Fact]
    public async Task UnidadesOrganizativas_ExposesWriteOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Check collection path exposes POST and GET
        var collectionPath = paths.GetProperty("/api/v1/unidades-organizativas");
        var collectionOps = new HashSet<string>();
        foreach (var op in collectionPath.EnumerateObject())
            collectionOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("post", collectionOps);
        Assert.Contains("get", collectionOps);

        // Check item path exposes GET, PUT, DELETE
        var itemPath = paths.GetProperty("/api/v1/unidades-organizativas/{id}");
        var itemOps = new HashSet<string>();
        foreach (var op in itemPath.EnumerateObject())
            itemOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("get", itemOps);
        Assert.Contains("put", itemOps);
        Assert.Contains("delete", itemOps);

        // Check parent-change path exposes PATCH
        var parentPath = paths.GetProperty("/api/v1/unidades-organizativas/{id}/unidad-padre");
        var parentOps = new HashSet<string>();
        foreach (var op in parentPath.EnumerateObject())
            parentOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", parentOps);

        // Check reactivar path exposes PATCH
        var reactivarPath = paths.GetProperty("/api/v1/unidades-organizativas/{id}/reactivar");
        var reactivarOps = new HashSet<string>();
        foreach (var op in reactivarPath.EnumerateObject())
            reactivarOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", reactivarOps);
    }

    [Fact]
    public async Task Puestos_ExposesWriteOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Check collection path exposes POST and GET
        var collectionPath = paths.GetProperty("/api/v1/puestos");
        var collectionOps = new HashSet<string>();
        foreach (var op in collectionPath.EnumerateObject())
            collectionOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("post", collectionOps);
        Assert.Contains("get", collectionOps);

        // Check item path exposes GET, PUT, DELETE
        var itemPath = paths.GetProperty("/api/v1/puestos/{id}");
        var itemOps = new HashSet<string>();
        foreach (var op in itemPath.EnumerateObject())
            itemOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("get", itemOps);
        Assert.Contains("put", itemOps);
        Assert.Contains("delete", itemOps);

        // Check reactivar path exposes PATCH
        var reactivarPath = paths.GetProperty("/api/v1/puestos/{id}/reactivar");
        var reactivarOps = new HashSet<string>();
        foreach (var op in reactivarPath.EnumerateObject())
            reactivarOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", reactivarOps);
    }

    [Fact]
    public async Task Skills_ExposesWriteOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Check collection path exposes POST and GET
        var collectionPath = paths.GetProperty("/api/v1/skills");
        var collectionOps = new HashSet<string>();
        foreach (var op in collectionPath.EnumerateObject())
            collectionOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("post", collectionOps);
        Assert.Contains("get", collectionOps);

        // Check item path exposes GET, PUT, DELETE
        var itemPath = paths.GetProperty("/api/v1/skills/{id}");
        var itemOps = new HashSet<string>();
        foreach (var op in itemPath.EnumerateObject())
            itemOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("get", itemOps);
        Assert.Contains("put", itemOps);
        Assert.Contains("delete", itemOps);

        // Check reactivar path exposes PATCH
        var reactivarPath = paths.GetProperty("/api/v1/skills/{id}/reactivar");
        var reactivarOps = new HashSet<string>();
        foreach (var op in reactivarPath.EnumerateObject())
            reactivarOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", reactivarOps);
    }

    [Fact]
    public async Task SkillSubresources_AreDocumented()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Cargo skill subresource: collection path lists assignments
        var cargoCollectionPath = paths.GetProperty("/api/v1/cargos/{cargoId}/skills");
        var cargoCollectionOps = new HashSet<string>();
        foreach (var op in cargoCollectionPath.EnumerateObject())
            cargoCollectionOps.Add(op.Name.ToLowerInvariant());
        Assert.Contains("get", cargoCollectionOps);

        // Cargo skill subresource: item path upserts/deletes an assignment
        var cargoItemPath = paths.GetProperty("/api/v1/cargos/{cargoId}/skills/{skillId}");
        var cargoItemOps = new HashSet<string>();
        foreach (var op in cargoItemPath.EnumerateObject())
            cargoItemOps.Add(op.Name.ToLowerInvariant());
        Assert.Contains("put", cargoItemOps);
        Assert.Contains("delete", cargoItemOps);

        // Persona skill subresource: collection path lists assignments
        var personaCollectionPath = paths.GetProperty("/api/v1/personas/{personaId}/skills");
        var personaCollectionOps = new HashSet<string>();
        foreach (var op in personaCollectionPath.EnumerateObject())
            personaCollectionOps.Add(op.Name.ToLowerInvariant());
        Assert.Contains("get", personaCollectionOps);

        // Persona skill subresource: item path upserts/deletes an assignment
        var personaItemPath = paths.GetProperty("/api/v1/personas/{personaId}/skills/{skillId}");
        var personaItemOps = new HashSet<string>();
        foreach (var op in personaItemPath.EnumerateObject())
            personaItemOps.Add(op.Name.ToLowerInvariant());
        Assert.Contains("put", personaItemOps);
        Assert.Contains("delete", personaItemOps);
    }

    [Fact]
    public async Task SkillsCatalog_DocumentsOnlyCatalogOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Collection path: catalog list and create
        var collectionPath = paths.GetProperty("/api/v1/skills");
        var collectionOps = new HashSet<string>();
        foreach (var op in collectionPath.EnumerateObject())
            collectionOps.Add(op.Name.ToLowerInvariant());
        Assert.Contains("get", collectionOps);
        Assert.Contains("post", collectionOps);

        // Item path: catalog read, update, delete
        var itemPath = paths.GetProperty("/api/v1/skills/{id}");
        var itemOps = new HashSet<string>();
        foreach (var op in itemPath.EnumerateObject())
            itemOps.Add(op.Name.ToLowerInvariant());
        Assert.Contains("get", itemOps);
        Assert.Contains("put", itemOps);
        Assert.Contains("delete", itemOps);

        // Reactivar path: catalog reactivate
        var reactivarPath = paths.GetProperty("/api/v1/skills/{id}/reactivar");
        var reactivarOps = new HashSet<string>();
        foreach (var op in reactivarPath.EnumerateObject())
            reactivarOps.Add(op.Name.ToLowerInvariant());
        Assert.Contains("patch", reactivarOps);

        // No other paths should live under /api/v1/skills (e.g. assignment endpoints).
        // /consulta es la ruta segmentada y es esperada para este módulo.
        // /{skillId}/cargos es el subrecurso readonly skill→cargos del change
        // `habilidades-navegacion-cargos` (skill-cargo-query-contract).
        // Adicionalmente, el subrecurso MUST exponer solo operaciones GET
        // (skill-cargo-query-contract Req 4 — contrato readonly); bloquear
        // aquí evita que un POST/PUT/DELETE silencioso quede en el catálogo
        // sin enforcement de tests.
        foreach (var path in paths.EnumerateObject())
        {
            if (path.Name.StartsWith("/api/v1/skills", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(
                    path.Name is "/api/v1/skills"
                        or "/api/v1/skills/{id}"
                        or "/api/v1/skills/{id}/reactivar"
                        or "/api/v1/skills/consulta"
                        or "/api/v1/skills/{skillId}/cargos"
                        or "/api/v1/skills/{skillId}/personas",
                    $"Unexpected skill catalog sub-path documented: {path.Name}");

                if (string.Equals(path.Name, "/api/v1/skills/{skillId}/cargos", StringComparison.OrdinalIgnoreCase))
                {
                    var ops = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var op in path.Value.EnumerateObject())
                    {
                        ops.Add(op.Name);
                    }

                    Assert.Equal(
                        new[] { "get" },
                        ops.ToArray(),
                        StringComparer.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public async Task SkillGetSchemas_DocumentEnrichedNestedData()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");

        // CargoSkillDetailDto must exist and document skill, nivel (with nested IDs, not root-level skillId/nivelId)
        var cargoDetailSchema = schemas.GetProperty("CargoSkillDetailDto");
        var cargoDetailProps = cargoDetailSchema.GetProperty("properties");
        Assert.True(cargoDetailProps.TryGetProperty("skill", out _), "CargoSkillDetailDto MUST have 'skill'");
        Assert.True(cargoDetailProps.TryGetProperty("nivel", out _), "CargoSkillDetailDto MUST have 'nivel'");

        // PersonaSkillDetailDto must exist and document skill, nivel (with nested IDs, not root-level skillId/nivelId)
        var personaDetailSchema = schemas.GetProperty("PersonaSkillDetailDto");
        var personaDetailProps = personaDetailSchema.GetProperty("properties");
        Assert.True(personaDetailProps.TryGetProperty("skill", out _), "PersonaSkillDetailDto MUST have 'skill'");
        Assert.True(personaDetailProps.TryGetProperty("nivel", out _), "PersonaSkillDetailDto MUST have 'nivel'");

        // Write schemas (CargoSkillDto and PersonaSkillDto) must still be documented
        Assert.True(schemas.TryGetProperty("CargoSkillDto", out _), "Write contract CargoSkillDto MUST still be documented");
        Assert.True(schemas.TryGetProperty("PersonaSkillDto", out _), "Write contract PersonaSkillDto MUST still be documented");
    }

    [Fact]
    public async Task CargoSkillDetailDto_ExponeNivelRequeridoIdPonderacionEsObligatoriaSinAliasNivelId()
    {
        // PR2-T2.3: el contrato de lectura del subrecurso debe exponer
        // explícitamente nivelRequeridoId, ponderacion y esObligatoria,
        // y NO debe mantener el alias legado 'nivelId' (cargo-skill-query-contract Req 1 y 3).
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var schema = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CargoSkillDetailDto");
        var props = schema.GetProperty("properties");

        Assert.True(props.TryGetProperty("nivelRequeridoId", out var nivelRequeridoIdProp),
            "CargoSkillDetailDto MUST expose 'nivelRequeridoId'");
        Assert.Equal("string", nivelRequeridoIdProp.GetProperty("type").GetString());
        Assert.Equal("uuid", nivelRequeridoIdProp.GetProperty("format").GetString());

        Assert.True(props.TryGetProperty("ponderacion", out var ponderacionProp),
            "CargoSkillDetailDto MUST expose 'ponderacion'");
        Assert.Equal("number", ponderacionProp.GetProperty("type").GetString());

        Assert.True(props.TryGetProperty("esObligatoria", out var esObligatoriaProp),
            "CargoSkillDetailDto MUST expose 'esObligatoria'");
        Assert.Equal("boolean", esObligatoriaProp.GetProperty("type").GetString());

        Assert.True(props.TryGetProperty("skillId", out _),
            "CargoSkillDetailDto MUST also expose 'skillId' for editable table binding");

        // El alias legado 'nivelId' NO debe existir en el contrato de lectura
        // del subrecurso de skills — solo 'nivelRequeridoId' (cargo-skill-query-contract nota final).
        Assert.False(props.TryGetProperty("nivelId", out _),
            "CargoSkillDetailDto MUST NOT expose the legacy 'nivelId' alias");
    }

    [Fact]
    public async Task CargoSkillDetailDto_NivelAnidadoExponeIdNoNivelId()
    {
        // PR2-T2.3: el schema del objeto 'nivel' anidado usa el campo
        // canónico 'id' (proveniente de NivelHabilidadDto.Id), no 'nivelId'.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var schema = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CargoSkillDetailDto");
        var nivelRef = schema.GetProperty("properties").GetProperty("nivel");
        Assert.Equal("#/components/schemas/NivelHabilidadDto", nivelRef.GetProperty("$ref").GetString());

        var nivelSchema = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("NivelHabilidadDto");
        var nivelProps = nivelSchema.GetProperty("properties");
        Assert.True(nivelProps.TryGetProperty("id", out _),
            "NivelHabilidadDto MUST expose 'id'");
        Assert.False(nivelProps.TryGetProperty("nivelId", out _),
            "NivelHabilidadDto MUST NOT expose a legacy 'nivelId' property");
    }

    [Fact]
    public async Task CargoSkillSubresourceGetOperation_DocumentsEnrichedResponse()
    {
        // PR2-T2.3: el operation GET /api/v1/cargos/{cargoId}/skills
        // referencia explícitamente el schema CargoSkillDetailDto (no
        // un payload distinto que omita los nuevos campos).
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var getOp = paths.GetProperty("/api/v1/cargos/{cargoId}/skills").GetProperty("get");
        var okResponse = getOp.GetProperty("responses").GetProperty("200");
        var schemaRef = okResponse.GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("items").GetProperty("$ref").GetString();
        Assert.Equal("#/components/schemas/CargoSkillDetailDto", schemaRef);
    }

    [Fact]
    public async Task CargoDto_NoContaminaCamposDelSubrecursoSkill()
    {
        // PR2-T2.4: el schema del padre CargoDto no debe contener los
        // campos introducidos por el subrecurso /skills
        // (cargo-skill-query-contract Req 3 escenario "No contaminar el
        // contrato padre de Cargo").
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var schema = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CargoDto");
        var props = schema.GetProperty("properties");

        // Campos propios del padre que SÍ deben existir.
        Assert.True(props.TryGetProperty("id", out _), "CargoDto MUST expose 'id'");
        Assert.True(props.TryGetProperty("codigo", out _), "CargoDto MUST expose 'codigo'");
        Assert.True(props.TryGetProperty("nombre", out _), "CargoDto MUST expose 'nombre'");
        Assert.True(props.TryGetProperty("nivelId", out _), "CargoDto MUST expose 'nivelId' (NivelCargo FK)");

        // Campos del subrecurso que NO deben contaminar al padre.
        Assert.False(props.TryGetProperty("nivelRequeridoId", out _),
            "CargoDto MUST NOT expose 'nivelRequeridoId' (lives in CargoSkillDetailDto)");
        Assert.False(props.TryGetProperty("ponderacion", out _),
            "CargoDto MUST NOT expose 'ponderacion' (lives in CargoSkillDetailDto)");
        Assert.False(props.TryGetProperty("esObligatoria", out _),
            "CargoDto MUST NOT expose 'esObligatoria' (lives in CargoSkillDetailDto)");
        Assert.False(props.TryGetProperty("skillId", out _),
            "CargoDto MUST NOT expose 'skillId' (lives in CargoSkillDetailDto)");
        Assert.False(props.TryGetProperty("habilidades", out _),
            "CargoDto MUST NOT expose 'habilidades' array");
    }

    [Fact]
    public async Task SwaggerDocument_NoCargoHabilidadOrPersonaHabilidadPaths()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var pathNames = string.Join('\n', doc.RootElement.GetProperty("paths").EnumerateObject().Select(path => path.Name));

        // Must NOT document direct CargoHabilidad or PersonaHabilidad aggregate endpoints.
        Assert.DoesNotContain("CargoHabilidad", pathNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PersonaHabilidad", pathNames, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Personas_ExposesWriteOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Check collection path exposes POST and GET
        var collectionPath = paths.GetProperty("/api/v1/personas");
        var collectionOps = new HashSet<string>();
        foreach (var op in collectionPath.EnumerateObject())
            collectionOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("post", collectionOps);
        Assert.Contains("get", collectionOps);

        // Check item path exposes GET, PUT, DELETE
        var itemPath = paths.GetProperty("/api/v1/personas/{id}");
        var itemOps = new HashSet<string>();
        foreach (var op in itemPath.EnumerateObject())
            itemOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("get", itemOps);
        Assert.Contains("put", itemOps);
        Assert.Contains("delete", itemOps);

        // Check reactivar path exposes PATCH
        var reactivarPath = paths.GetProperty("/api/v1/personas/{id}/reactivar");
        var reactivarOps = new HashSet<string>();
        foreach (var op in reactivarPath.EnumerateObject())
            reactivarOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", reactivarOps);
    }

    [Fact]
    public async Task Ocupaciones_ExposesWriteOperations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        // Check collection path exposes POST and GET
        var collectionPath = paths.GetProperty("/api/v1/ocupaciones");
        var collectionOps = new HashSet<string>();
        foreach (var op in collectionPath.EnumerateObject())
            collectionOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("post", collectionOps);
        Assert.Contains("get", collectionOps);

        // Check item path exposes GET, PUT, DELETE
        var itemPath = paths.GetProperty("/api/v1/ocupaciones/{id}");
        var itemOps = new HashSet<string>();
        foreach (var op in itemPath.EnumerateObject())
            itemOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("get", itemOps);
        Assert.Contains("put", itemOps);
        Assert.Contains("delete", itemOps);

        // Check finalizar path exposes PATCH
        var finalizarPath = paths.GetProperty("/api/v1/ocupaciones/{id}/finalizar");
        var finalizarOps = new HashSet<string>();
        foreach (var op in finalizarPath.EnumerateObject())
            finalizarOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", finalizarOps);

        // Check reactivar path exposes PATCH
        var reactivarPath = paths.GetProperty("/api/v1/ocupaciones/{id}/reactivar");
        var reactivarOps = new HashSet<string>();
        foreach (var op in reactivarPath.EnumerateObject())
            reactivarOps.Add(op.Name.ToLowerInvariant());

        Assert.Contains("patch", reactivarOps);
    }

    [Fact]
    public async Task ConsultaEndpoint_DocumentaParametroStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var consultaPath = paths.GetProperty("/api/v1/unidades-organizativas/consulta");
        var getOp = consultaPath.GetProperty("get");
        var parameters = getOp.GetProperty("parameters");

        // Find the status parameter among declared parameters
        var statusParam = parameters.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("name").GetString() == "status");

        Assert.NotEqual(default, statusParam);
        Assert.Equal("query", statusParam.GetProperty("in").GetString());
    }

    [Fact]
    public async Task ConsultaEndpoint_StatusParameter_DocumentaValoresActivasYEliminadas()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var consultaPath = paths.GetProperty("/api/v1/unidades-organizativas/consulta");
        var getOp = consultaPath.GetProperty("get");
        var parameters = getOp.GetProperty("parameters");

        var statusParam = parameters.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("name").GetString() == "status");

        Assert.NotEqual(default, statusParam);

        // Check schema/description documents the enum values
        var description = statusParam.GetProperty("description").GetString() ?? "";
        Assert.Contains("activas", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eliminadas", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("por defecto", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsultaEndpoint_ResponseSchema_ReusesUnidadOrganizativaDtoForDeletedView()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var consultaGet = root.GetProperty("paths")
            .GetProperty("/api/v1/unidades-organizativas/consulta")
            .GetProperty("get");

        var responseSchema = consultaGet.GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        var pagedSchemaRef = responseSchema.GetProperty("$ref").GetString();
        Assert.False(string.IsNullOrWhiteSpace(pagedSchemaRef));

        var pagedSchemaName = pagedSchemaRef!.Split('/').Last();
        var pagedSchema = root.GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(pagedSchemaName);

        var itemRef = pagedSchema.GetProperty("properties")
            .GetProperty("items")
            .GetProperty("items")
            .GetProperty("$ref")
            .GetString();

        Assert.Equal("#/components/schemas/UnidadOrganizativaDto", itemRef);
    }

    [Fact]
    public async Task ConsultaEndpoint_ResponseDescription_StatesDeletedViewKeepsSameContractWithoutMixedResults()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var responseDescription = doc.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/unidades-organizativas/consulta")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("description")
            .GetString() ?? string.Empty;

        Assert.Contains("UnidadOrganizativaDto", responseDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eliminadas", responseDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no mezcla", responseDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsultaEndpoint_StatusParameter_NoApareceEnArbol()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var arbolPath = paths.GetProperty("/api/v1/unidades-organizativas/arbol");
        var getOp = arbolPath.GetProperty("get");

        // The arbol endpoint should NOT have a status parameter
        Assert.False(getOp.TryGetProperty("parameters", out _),
            "Tree endpoint should not declare any parameters");
    }

    [Fact]
    public async Task Cargos_ConsultaEndpoint_DocumentaParametroStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/cargos/consulta", out var consultaPath),
            "Swagger MUST document GET /api/v1/cargos/consulta");
        var getOp = consultaPath.GetProperty("get");
        var parameters = getOp.GetProperty("parameters");

        var statusParam = parameters.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("name").GetString() == "status");

        Assert.NotEqual(default, statusParam);
        Assert.Equal("query", statusParam.GetProperty("in").GetString());
    }

    [Fact]
    public async Task Cargos_ReactivarEndpoint_SigueDocumentado()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var reactivarPath = paths.GetProperty("/api/v1/cargos/{id}/reactivar");
        var patchOp = reactivarPath.GetProperty("patch");

        // Verifica que el endpoint PATCH /api/v1/cargos/{id}/reactivar sigue documentado.
        Assert.True(patchOp.TryGetProperty("responses", out _));
    }

    // ---- Habilidades / NivelesHabilidad discoverability ----

    [Fact]
    public async Task DiscoverSkillsConsultaEndpoint_Test()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/skills/consulta", out var consultaPath),
            "Swagger MUST document GET /api/v1/skills/consulta");
        var getOp = consultaPath.GetProperty("get");

        // El método GET debe estar documentado con sus responses 200/401.
        Assert.True(getOp.TryGetProperty("responses", out var responses));
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("401", out _));

        // El parámetro status debe estar declarado como query string.
        var parameters = getOp.GetProperty("parameters");
        var statusParam = parameters.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("name").GetString() == "status");

        Assert.NotEqual(default, statusParam);
        Assert.Equal("query", statusParam.GetProperty("in").GetString());
    }

    [Fact]
    public async Task DiscoverNivelesHabilidadEndpoint_Test()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/niveles-habilidad", out var nivelPath),
            "Swagger MUST document GET /api/v1/niveles-habilidad");
        var getOp = nivelPath.GetProperty("get");

        Assert.True(getOp.TryGetProperty("responses", out var responses));
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("401", out _));
    }

    [Fact]
    public async Task Habilidades_ConsultaEndpoint_StatusParameter_DocumentaValoresActivasYEliminadas()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var paths = doc.RootElement.GetProperty("paths");
        var consultaPath = paths.GetProperty("/api/v1/skills/consulta");
        var getOp = consultaPath.GetProperty("get");
        var parameters = getOp.GetProperty("parameters");

        var statusParam = parameters.EnumerateArray()
            .FirstOrDefault(p => p.GetProperty("name").GetString() == "status");

        Assert.NotEqual(default, statusParam);

        var description = statusParam.GetProperty("description").GetString() ?? "";
        Assert.Contains("activas", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eliminadas", description, StringComparison.OrdinalIgnoreCase);
    }
}
