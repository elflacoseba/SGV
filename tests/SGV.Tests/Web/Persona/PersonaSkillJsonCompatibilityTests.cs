using System.Text.Json;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Anti-drift JSON para los wire-types <c>PersonaSkill*</c> migrados a
/// <c>SGV.Contracts.Personas</c> (slice 1 / REQ-TAXO-01, SCENARIO-01).
///
/// <para>
/// La migración de <c>SGV.Aplicacion</c> a <c>SGV.Contracts</c> debe
/// preservar el shape JSON observable: el endpoint
/// <c>GET /api/v1/personas/{id}/skills</c> sigue exponiendo la lista
/// anidada <c>{skill:{...},nivel:{...}}</c>, y los write contracts
/// (<c>PersonaSkillDto</c>, <c>AsignarPersonaSkillRequest</c>) siguen
/// usando <c>skillId</c>/<c>nivelId</c> planos. Estos tests son
/// regression anti-drift: si alguien cambia el casing, agrega/quita
/// propiedades o restructura a un DTO plano, el wire JSON queda
/// protegido.
/// </para>
/// </summary>
public sealed class PersonaSkillJsonCompatibilityTests
{
    private static readonly Guid SkillId = Guid.Parse("62000000-0000-0000-0000-000000000001");
    private static readonly Guid NivelId = Guid.Parse("63000000-0000-0000-0000-000000000001");

    [Fact]
    public void PersonaSkillDto_SerializesWithCamelCaseSkillIdAndNivelId()
    {
        var dto = new PersonaSkillDto(SkillId, NivelId);

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("skillId", out var skillIdProp),
            "PersonaSkillDto MUST serialize 'skillId' (legacy/active wire shape).");
        Assert.Equal(SkillId, skillIdProp.GetGuid());

        Assert.True(root.TryGetProperty("nivelId", out var nivelIdProp),
            "PersonaSkillDto MUST serialize 'nivelId' (legacy/active wire shape).");
        Assert.Equal(NivelId, nivelIdProp.GetGuid());

        Assert.False(root.TryGetProperty("nivelHabilidadId", out _),
            "PersonaSkillDto MUST NOT expose the internal 'nivelHabilidadId' alias.");
    }

    [Fact]
    public void PersonaSkillDetailDto_SerializesWithNestedSkillAndNivel()
    {
        var skill = new HabilidadDto(SkillId, "PROG", "Programación", null, "Técnica");
        var nivel = new NivelHabilidadDto(NivelId, "N1", "Nivel 1", 1, 1);
        var detail = new PersonaSkillDetailDto(skill, nivel);

        var json = JsonSerializer.Serialize(detail, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("skill", out var skillProp),
            "PersonaSkillDetailDto MUST nest the habilidad under 'skill'.");
        Assert.True(skillProp.TryGetProperty("id", out var skillIdProp),
            "Nested 'skill' MUST expose 'id'.");
        Assert.Equal(SkillId, skillIdProp.GetGuid());

        Assert.True(root.TryGetProperty("nivel", out var nivelProp),
            "PersonaSkillDetailDto MUST nest the nivel under 'nivel'.");
        Assert.True(nivelProp.TryGetProperty("id", out var nivelIdProp),
            "Nested 'nivel' MUST expose 'id'.");
        Assert.Equal(NivelId, nivelIdProp.GetGuid());

        Assert.False(root.TryGetProperty("skillId", out _),
            "PersonaSkillDetailDto MUST NOT expose flat 'skillId' at root.");
        Assert.False(root.TryGetProperty("nivelId", out _),
            "PersonaSkillDetailDto MUST NOT expose flat 'nivelId' at root.");
    }

    [Fact]
    public void AsignarPersonaSkillRequest_SerializesWithCamelCaseNivelId()
    {
        var request = new AsignarPersonaSkillRequest(NivelId);

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("nivelId", out var prop),
            "AsignarPersonaSkillRequest MUST serialize 'nivelId' (legacy wire shape).");
        Assert.Equal(NivelId, prop.GetGuid());

        Assert.False(root.TryGetProperty("nivelHabilidadId", out _),
            "AsignarPersonaSkillRequest MUST NOT expose the internal 'nivelHabilidadId' alias.");
    }

    [Fact]
    public void PersonaSkillDto_DeserializesPreservingCamelCaseKeys()
    {
        const string json = """{"skillId":"62000000-0000-0000-0000-000000000001","nivelId":"63000000-0000-0000-0000-000000000001"}""";

        var dto = JsonSerializer.Deserialize<PersonaSkillDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(SkillId, dto!.SkillId);
        Assert.Equal(NivelId, dto.NivelId);
    }

    [Fact]
    public void AsignarPersonaSkillRequest_DeserializesPreservingCamelCaseKey()
    {
        const string json = """{"nivelId":"63000000-0000-0000-0000-000000000001"}""";

        var request = JsonSerializer.Deserialize<AsignarPersonaSkillRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(NivelId, request!.NivelId);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
