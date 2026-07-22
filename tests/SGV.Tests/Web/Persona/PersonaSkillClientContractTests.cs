using System.Linq;
using System.Reflection;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Aprobación de contrato del subrecurso <c>persona-skill</c> sobre
/// <see cref="IPersonaApiClient"/>.
///
/// Slice 2 del change <c>implementa-persona-habilidades</c>: la
/// interface gana tres métodos para el subrecurso (consulta, upsert y
/// baja). Estos tests son guards de contrato: si alguien borra un
/// método, le cambia el nombre, devuelve un tipo distinto o le renombra
/// un parámetro (e.g. <c>personaId</c> → <c>id</c>), el test falla
/// ANTES de que el cambio silencioso impacte la Razor Page de Slice 3a.
///
/// Las firmas exactas son lo que el PageModel consume vía dependency
/// injection; congelarlas aquí evita que un refactor "limpio" rompa la
/// integración sin disparar CI. Esto es contract approval testing
/// conforme al patrón "approval tests" del strict-tdd: capturas el
/// contrato actual con assertions concretos, sin tocar producción.
/// </summary>
public class PersonaSkillClientContractTests
{
    [Fact]
    public void Interface_ExposesGetSkillsAsyncWithExpectedSignature()
    {
        // AC REQ-WEB-04: el subrecurso GET /api/v1/personas/{id}/skills
        // devuelve el listado completo de habilidades de la persona con
        // su nivel; el cliente expone `GetSkillsAsync(Guid personaId, ...)`.
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.GetSkillsAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IReadOnlyList<PersonaSkillDetailDto>>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("personaId", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesUpsertSkillAsyncWithExpectedSignature()
    {
        // AC REQ-WEB-04: PUT /api/v1/personas/{personaId}/skills/{skillId}
        // con payload { nivelId } es la operación de upsert idempotente;
        // el cliente expone `UpsertSkillAsync(Guid personaId, Guid skillId, AsignarPersonaSkillRequest, ...)`.
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.UpsertSkillAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PersonaSkillCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(4, parameters.Length);
        Assert.Equal("personaId", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("skillId", parameters[1].Name);
        Assert.Equal(typeof(Guid), parameters[1].ParameterType);
        Assert.Equal("request", parameters[2].Name);
        Assert.Equal(typeof(AsignarPersonaSkillRequest), parameters[2].ParameterType);
        Assert.Equal("cancellationToken", parameters[3].Name);
        Assert.Equal(typeof(CancellationToken), parameters[3].ParameterType);
        Assert.True(parameters[3].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesDeleteSkillAsyncWithExpectedSignature()
    {
        // AC REQ-WEB-04: DELETE /api/v1/personas/{personaId}/skills/{skillId}
        // devuelve 204 No Content en éxito; el cliente expone
        // `DeleteSkillAsync(Guid personaId, Guid skillId, ...)`.
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.DeleteSkillAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PersonaSkillDeleteResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal("personaId", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("skillId", parameters[1].Name);
        Assert.Equal(typeof(Guid), parameters[1].ParameterType);
        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.True(parameters[2].HasDefaultValue);
    }

    [Fact]
    public void Interface_SubresourceMethodsAreExactlyThree()
    {
        // Defensa contra refactors "creativos" que sumen un cuarto método
        // (e.g. un BulkUpsert) sin actualizar el contrato documentado en
        // design.md. Si se agrega un método, este test obliga a actualizar
        // también las expectations explícitas de arriba.
        //
        // El subrecurso vive bajo dos sufijos: "SkillAsync" (singular, para
        // mutaciones de un único vínculo) y "SkillsAsync" (plural, para el
        // listado del GET). Ambos tienen que estar presentes.
        var mutationMethods = typeof(IPersonaApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("SkillAsync", StringComparison.Ordinal))
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "DeleteSkillAsync", "UpsertSkillAsync" },
            mutationMethods);

        // El listado plural vive bajo "SkillsAsync" (no "SkillAsync"), por
        // lo que se busca por separado para blindar el sufijo.
        var queryMethods = typeof(IPersonaApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("SkillsAsync", StringComparison.Ordinal))
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "GetSkillsAsync" }, queryMethods);
    }
}