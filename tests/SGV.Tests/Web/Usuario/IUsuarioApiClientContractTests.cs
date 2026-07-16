using System.Linq;
using System.Reflection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Usuarios;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Aprobación de contrato de <see cref="IUsuarioApiClient"/>.
///
/// <para>
/// La interface define ocho métodos introducidos en el change
/// <c>Implementa módulo usuarios</c>: <c>GetAllActivasAsync</c>,
/// <c>QueryAsync</c>, <c>GetByIdAsync</c>, <c>CreateAsync</c>,
/// <c>UpdateAsync</c>, <c>DesactivarAsync</c> (+ alias
/// <c>DeleteAsync</c> default-implemented), <c>ReactivarAsync</c> y
/// <c>GetRolesAsync</c>. Estos tests son guards de contrato: si alguien
/// borra un método, le cambia el nombre, devuelve un tipo distinto o le
/// renombra un parámetro (e.g. <c>id</c> → <c>userId</c>), el test
/// falla ANTES de que el cambio silencioso impacte las Razor Pages de
/// PR 3/4.
/// </para>
///
/// <para>
/// La forma <c>UsuarioDeleteResult</c> NO existe en PR 2 (se reutiliza
/// <see cref="UsuarioCommandResult"/> + un par de propiedades de éxito/fallo
/// para mantener el seam HTTP alineado con el shape de backend y evitar un
/// quinto record casi idéntico a PersonaDeleteResult). El guardrail vive
/// entonces en la firma de <c>DesactivarAsync</c> y su mapping al contrato
/// <c>CommandResult</c> común.
/// </para>
/// </summary>
public class IUsuarioApiClientContractTests
{
    [Fact]
    public void Interface_ExposesGetAllActivasAsyncWithExpectedSignature()
    {
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.GetAllActivasAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IReadOnlyList<UsuarioDto>>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("cancellationToken", parameters[0].Name);
        Assert.Equal(typeof(CancellationToken), parameters[0].ParameterType);
        Assert.True(parameters[0].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesQueryAsyncWithExpectedSignature()
    {
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.QueryAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<UsuarioListadoDto>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("query", parameters[0].Name);
        Assert.Equal(typeof(UsuarioListQuery), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesGetByIdAsyncWithExpectedSignature()
    {
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.GetByIdAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<UsuarioDto?>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesCreateAsyncWithExpectedSignature()
    {
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.CreateAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<UsuarioCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(CrearUsuarioRequest), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesUpdateAsyncWithExpectedSignature()
    {
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.UpdateAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<UsuarioCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("request", parameters[1].Name);
        Assert.Equal(typeof(ActualizarUsuarioRequest), parameters[1].ParameterType);
        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
        Assert.True(parameters[2].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesDesactivarAsyncWithExpectedSignature()
    {
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.DesactivarAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<UsuarioCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesReactivarAsyncWithExpectedSignature()
    {
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.ReactivarAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<UsuarioCommandResult>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("id", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesGetRolesAsyncWithExpectedSignature()
    {
        // Atajo preservado para PR3/4: GET /api/v1/usuarios/{userId}/roles.
        // No se introduce en este PR como flujo crítico pero la firma debe
        // existir desde ya para evitar un refactor de la página de roles
        // cuando llegue PR 4.
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.GetRolesAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IReadOnlyList<string>>), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("userId", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void Interface_ExposesExactlyNinePublicAsyncMethods()
    {
        // Defensa contra refactors "creativos" que sumen un nuevo método
        // (e.g. <c>BulkCreateAsync</c>) sin actualizar la suite de
        // contract tests. La cantidad esperada es 8 métodos "primary"
        // (GetAllActivasAsync, QueryAsync, GetByIdAsync, CreateAsync,
        // UpdateAsync, DesactivarAsync, ReactivarAsync, GetRolesAsync)
        // más el alias <c>DeleteAsync</c> default-implemented, total 9.
        var publicMethods = typeof(IUsuarioApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName) // excluye accessors
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "CreateAsync",
                "DeleteAsync",
                "DesactivarAsync",
                "GetAllActivasAsync",
                "GetByIdAsync",
                "GetRolesAsync",
                "QueryAsync",
                "ReactivarAsync",
                "UpdateAsync"
            },
            publicMethods);
    }
}
