using System.Reflection;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Contracts.Personas;

/// <summary>
/// Aprobación de contrato para la migración atómica de los wire-types
/// <c>PersonaSkill*</c> desde <c>SGV.Aplicacion.Personas</c> a
/// <c>SGV.Contracts.Personas.*</c> (slice 1 / REQ-TAXO-01, REQ-TAXO-03,
/// SCENARIO-01).
///
/// <para>
/// El cambio debe preservar la posición canónica de los wire-types en
/// <c>SGV.Contracts.Personas</c> para que <c>SGV.Api</c> y <c>SGV.Web</c>
/// compilen contra ese proyecto sin duplicar DTOs en
/// <c>SGV.Aplicacion</c>. Estos tests son guards de contrato: si alguien
/// borra el tipo, lo renombra o lo mueve fuera de <c>Contracts</c>, el
/// compile-time reference falla ANTES de que el cambio impacte el wire
/// shape vigente.
/// </para>
/// </summary>
public sealed class PersonaSkillContractsCompatibilityTests
{
    [Fact]
    public void Contracts_ExposesPersonaSkillCommandResult()
    {
        var type = typeof(PersonaSkillCommandResult);

        Assert.NotNull(type);
        Assert.Equal("PersonaSkillCommandResult", type.Name);
        Assert.Equal(
            typeof(SGV.Contracts.Personas.Comandos.PersonaSkillCommandResult).Assembly.GetName().Name,
            type.Assembly.GetName().Name);
    }

    [Fact]
    public void Contracts_ExposesPersonaSkillError()
    {
        var type = typeof(PersonaSkillError);

        Assert.NotNull(type);
        Assert.Equal("PersonaSkillError", type.Name);
    }

    [Fact]
    public void Contracts_ExposesPersonaSkillDeleteResult()
    {
        var type = typeof(PersonaSkillDeleteResult);

        Assert.NotNull(type);
        Assert.Equal("PersonaSkillDeleteResult", type.Name);
    }

    [Fact]
    public void Contracts_ExposesAsignarPersonaSkillRequest()
    {
        var type = typeof(AsignarPersonaSkillRequest);

        Assert.NotNull(type);
        Assert.Equal("AsignarPersonaSkillRequest", type.Name);
    }

    [Fact]
    public void Contracts_ExposesPersonaSkillDto()
    {
        var type = typeof(PersonaSkillDto);

        Assert.NotNull(type);
        Assert.Equal("PersonaSkillDto", type.Name);
    }

    [Fact]
    public void Contracts_ExposesPersonaSkillDetailDto()
    {
        var type = typeof(PersonaSkillDetailDto);

        Assert.NotNull(type);
        Assert.Equal("PersonaSkillDetailDto", type.Name);
    }

    [Fact]
    public void Contracts_PersonaSkillErrorTypeEnum_StillExists()
    {
        // El enum interno del subdominio PersonaSkill (NotFound, Validation)
        // queda en SGV.Contracts.Personas.Comandos; el cliente web NO debe
        // ramificar por este enum — usa PersonaSkillError.Categoria vía
        // CommandResultMapper.
        var type = typeof(SGV.Contracts.Personas.Comandos.PersonaSkillErrorType);

        Assert.NotNull(type);
        Assert.True(type.IsEnum);
        var names = Enum.GetNames(type);
        Assert.Contains("NotFound", names);
        Assert.Contains("Validation", names);
    }

    [Fact]
    public void Contracts_PersonaSkillError_ExposesCategoriaPropertyOfTypeErrorCategoria()
    {
        var property = typeof(PersonaSkillError).GetProperty(
            "Categoria",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(typeof(SGV.Contracts.Comun.ErrorCategoria), property!.PropertyType);
    }

    [Fact]
    public void Contracts_PersonaSkillError_ExposesStatusCodeNullableIntProperty()
    {
        // StatusCode es metadata observada por CommandResultMapper para
        // mapear errores que llegan sin Categoria explícita.
        var property = typeof(PersonaSkillError).GetProperty(
            "StatusCode",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(typeof(int?), property!.PropertyType);
    }
}
