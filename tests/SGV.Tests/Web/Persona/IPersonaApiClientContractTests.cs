using System.Linq;
using System.Reflection;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Aprobación de contrato de <see cref="IPersonaApiClient"/>.
///
/// La interface ya define siete métodos introducidos en los PRs 1-3 del
/// change <c>2026-07-14-frontend-crud-personas</c>: <c>GetAllAsync</c>,
/// <c>GetByIdAsync</c>, <c>DesactivarAsync</c> (+ alias <c>DeleteAsync</c>),
/// <c>CreateAsync</c>, <c>UpdateAsync</c>, <c>QueryAsync</c> y
/// <c>ReactivarAsync</c>. Estos tests son guards de contrato: si alguien
/// borra un método, le cambia el nombre, devuelve un tipo distinto o le
/// renombra un parámetro (e.g. <c>id</c> → <c>personaId</c>), el test
/// falla ANTES de que el cambio silencioso impacte las Razor Pages ya
/// mergeadas en PR 3/4.
/// </summary>
public class IPersonaApiClientContractTests
{
    [Fact]
    public void Interface_ExposesGetAllAsyncWithExpectedSignature()
    {
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.GetAllAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IReadOnlyList<PersonaDto>>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("cancellationToken", parameters[0].Name);
        Assert.Equal(typeof(CancellationToken), parameters[0].ParameterType);
        Assert.True(parameters[0].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesGetByIdAsyncWithExpectedSignature()
    {
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.GetByIdAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PersonaDto?>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesCreateAsyncWithExpectedSignature()
    {
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.CreateAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PersonaCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(CrearPersonaRequest), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesUpdateAsyncWithExpectedSignature()
    {
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.UpdateAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PersonaCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("request", parameters[1].Name);
        Assert.Equal(typeof(ActualizarPersonaRequest), parameters[1].ParameterType);
        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.True(parameters[2].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesQueryAsyncWithExpectedSignature()
    {
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.QueryAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PersonaListadoDto>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("query", parameters[0].Name);
        Assert.Equal(typeof(PersonaListQuery), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesReactivarAsyncWithExpectedSignature()
    {
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.ReactivarAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PersonaCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesGetTiposDocumentoAsyncWithExpectedSignature()
    {
        // AC issue #147 PR3: el shell web expone el catálogo de tipos de
        // documento al PageModel para popular el <select> en Create/Edit.
        // Espejo de los demás tests de firma.
        var method = typeof(IPersonaApiClient).GetMethod(nameof(IPersonaApiClient.GetTiposDocumentoAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IReadOnlyList<TipoDocumentoDto>>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("cancellationToken", parameters[0].Name);
        Assert.Equal(typeof(CancellationToken), parameters[0].ParameterType);
        Assert.True(parameters[0].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesExactlyTwelvePublicAsyncMethods()
    {
        // Defensa contra refactors "creativos" que sumen un nuevo método
        // (e.g. <c>BulkCreateAsync</c>) sin actualizar la suite de
        // contract tests. La cantidad esperada es 12: los 9 originales
        // (GetAllAsync, GetByIdAsync, GetTiposDocumentoAsync,
        // DesactivarAsync, CreateAsync, UpdateAsync, QueryAsync,
        // ReactivarAsync + el alias default-implemented DeleteAsync) más
        // los 3 del subrecurso persona-skill agregados en Slice 2 del
        // change implementa-persona-habilidades (GetSkillsAsync,
        // UpsertSkillAsync, DeleteSkillAsync).
        //
        // El alias <c>DeleteAsync</c> es un default interface method, así
        // que aparece también en la lista.
        var publicMethods = typeof(IPersonaApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName) // excluye accessors
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] {
                "CreateAsync",
                "DeleteAsync",
                "DeleteSkillAsync",
                "DesactivarAsync",
                "GetAllAsync",
                "GetByIdAsync",
                "GetSkillsAsync",
                "GetTiposDocumentoAsync",
                "QueryAsync",
                "ReactivarAsync",
                "UpdateAsync",
                "UpsertSkillAsync"
            },
            publicMethods);
    }
}