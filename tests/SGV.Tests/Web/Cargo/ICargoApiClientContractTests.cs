using System.Linq;
using System.Reflection;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Aprobación de contrato del subrecurso <c>cargo-skill</c> sobre
/// <see cref="ICargoApiClient"/>.
///
/// La interface ya define tres métodos para el subrecurso (introducidos en
/// <c>941b705e feat(web): extend ICargoApiClient with cargo-skill
/// subresource methods</c>) y la implementación llegó en
/// <c>c3bc2743 feat(web): implement cargo-skill subresource methods on
/// CargoApiClient</c>. Estos tests son guards de contrato: si alguien borra
/// un método, le cambia el nombre, devuelve un tipo distinto o le renombra
/// un parámetro (e.g. <c>cargoId</c> → <c>id</c>), el test falla ANTES de
/// que el cambio silencioso impacte la Razor Page de PR3b.
///
/// Las firmas exactas son lo que la Razor Page consume vía dependency
/// injection; congelarlas aquí evita que un refactor "limpio" rompa la
/// integración sin disparar CI. Esto es contract approval testing conforme
/// al patrón "approval tests" del strict-tdd: capturas el contrato actual
/// con assertions concretos, sin tocar producción.
/// </summary>
public class ICargoApiClientContractTests
{
    [Fact]
    public void Interface_ExposesGetSkillsAsyncWithExpectedSignature()
    {
        var method = typeof(ICargoApiClient).GetMethod(nameof(ICargoApiClient.GetSkillsAsync));

        Assert.NotNull(method);

        Assert.Equal(typeof(Task<IReadOnlyList<CargoSkillDetailDto>>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("cargoId", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesUpsertSkillAsyncWithExpectedSignature()
    {
        var method = typeof(ICargoApiClient).GetMethod(nameof(ICargoApiClient.UpsertSkillAsync));

        Assert.NotNull(method);

        Assert.Equal(typeof(Task<CargoSkillCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(4, parameters.Length);
        Assert.Equal("cargoId", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("skillId", parameters[1].Name);
        Assert.Equal(typeof(Guid), parameters[1].ParameterType);
        Assert.Equal("request", parameters[2].Name);
        Assert.Equal(typeof(AsignarCargoSkillRequest), parameters[2].ParameterType);
        Assert.Equal("cancellationToken", parameters[3].Name);
        Assert.Equal(typeof(CancellationToken), parameters[3].ParameterType);
        Assert.True(parameters[3].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesDeleteSkillAsyncWithExpectedSignature()
    {
        var method = typeof(ICargoApiClient).GetMethod(nameof(ICargoApiClient.DeleteSkillAsync));

        Assert.NotNull(method);

        Assert.Equal(typeof(Task<CargoSkillDeleteResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal("cargoId", parameters[0].Name);
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
        var mutationMethods = typeof(ICargoApiClient)
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
        var queryMethods = typeof(ICargoApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("SkillsAsync", StringComparison.Ordinal))
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "GetSkillsAsync" }, queryMethods);
    }
}
