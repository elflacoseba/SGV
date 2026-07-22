using System.Text.Json;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Contracts.Habilidades;

public sealed class SkillPersonaContractsCompatibilityTests
{
    [Fact]
    public void SkillPersonaDetailDto_SerializesExpectedJsonProperties()
    {
        var personaId = Guid.NewGuid();
        var habilidadId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var dto = new SkillPersonaDetailDto(
            new PersonaDto(personaId, "L-1", "Ana", "Pérez", "ana@example.test", null, null, null, null, null, true),
            new NivelHabilidadDto(nivelId, "BASICO", "Básico", 1, 1))
        {
            PersonaId = personaId,
            HabilidadId = habilidadId,
            NivelHabilidadId = nivelId
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.True(json.RootElement.TryGetProperty("persona", out _));
        Assert.True(json.RootElement.TryGetProperty("nivel", out _));
        Assert.Equal(personaId, json.RootElement.GetProperty("personaId").GetGuid());
        Assert.Equal(habilidadId, json.RootElement.GetProperty("habilidadId").GetGuid());
        Assert.Equal(nivelId, json.RootElement.GetProperty("nivelHabilidadId").GetGuid());
    }

    [Fact]
    public void QueryAndPageResult_ExposePersonaSegmentAndPaginationMetadata()
    {
        var query = new HabilidadPersonasListQuery(2, 25, "ana", "apellidos_desc", PersonaSegmentoListado.Eliminadas);
        var result = new PersonaHabilidadesPageResult([], query.Page, query.PageSize, 0, query.Sort, query.Segmento);

        Assert.Equal(2, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(0, result.Total);
        Assert.Equal("apellidos_desc", result.Sort);
        Assert.Equal(PersonaSegmentoListado.Eliminadas, result.Segmento);
        Assert.Empty(result.Items);
    }
}
