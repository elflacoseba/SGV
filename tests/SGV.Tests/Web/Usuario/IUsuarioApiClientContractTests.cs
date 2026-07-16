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
/// La interface define siete métodos introducidos en el change
/// <c>Implementa módulo usuarios</c>: <c>GetAllActivasAsync</c>,
/// <c>QueryAsync</c>, <c>GetByIdAsync</c>, <c>CreateAsync</c>,
/// <c>UpdateAsync</c>, <c>DesactivarAsync</c> (+ alias
/// <c>DeleteAsync</c> default-implemented) y <c>ReactivarAsync</c>.
/// El atajo <c>GetRolesAsync</c> fue retirado por el review de PR #148
/// porque apuntaba a <c>GET /api/v1/usuarios/{userId}/roles</c>, ruta
/// que nunca existió en el backend. Estos tests son guards de contrato:
/// si alguien borra un método, le cambia el nombre, devuelve un tipo
/// distinto o le renombra un parámetro (e.g. <c>id</c> → <c>userId</c>),
/// el test falla ANTES de que el cambio silencioso impacte las Razor
/// Pages de PR 3/4.
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
    public void Interface_DoesNotExposeDesactivarAsync()
    {
        // Phase 3 / change 2026-07-15-quita-soft-delete-usuario: la baja
        // lógica (Desactivar) se reemplazó por hard-delete (Eliminar).
        // El cliente Web debe reflejar esa baja y dejar de exponer el
        // método. Este guard evita un refactor silencioso que lo
        // restaure.
        var method = typeof(IUsuarioApiClient).GetMethod("DesactivarAsync");

        Assert.Null(method);
    }

    [Fact]
    public void Interface_ExposesBloquearAsyncWithExpectedSignature()
    {
        // Phase 3 / change 2026-07-15-quita-soft-delete-usuario:
        // BloquearAsync reemplaza la baja lógica; mapea a POST /{id}/bloquear.
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.BloquearAsync));

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
    public void Interface_ExposesDesbloquearAsyncWithExpectedSignature()
    {
        // Phase 3 / change 2026-07-15-quita-soft-delete-usuario:
        // DesbloquearAsync mapea a POST /{id}/desbloquear.
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.DesbloquearAsync));

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
    public void Interface_ExposesEliminarAsyncWithExpectedSignature()
    {
        // Phase 3 / change 2026-07-15-quita-soft-delete-usuario:
        // EliminarAsync reemplaza a DeleteAsync (que era alias de
        // DesactivarAsync). Mapea a DELETE /{id} (204 No Content).
        var method = typeof(IUsuarioApiClient).GetMethod(nameof(IUsuarioApiClient.EliminarAsync));

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
    public void Interface_DoesNotExposeReactivarAsync()
    {
        // Phase 2 del change 2026-07-15-quita-soft-delete-usuario
        // retiró PATCH /api/v1/usuarios/{id}/reactivar del backend.
        // El cliente Web debe reflejar esa baja y dejar de exponer el
        // método. Este guard evita un refactor silencioso que lo
        // restaure.
        var method = typeof(IUsuarioApiClient).GetMethod("ReactivarAsync");

        Assert.Null(method);
    }

    [Fact]
    public void Interface_DoesNotExposeGetRolesAsync()
    {
        // PR #148 review: GET /api/v1/usuarios/{userId}/roles nunca
        // existió en el controller — la única ruta vigente es
        // GET /api/v1/usuarios/roles (catálogo global) +
        // PUT /api/v1/usuarios/{userId}/roles (asignación). El método
        // GetRolesAsync del cliente queda como dead code y se elimina.
        // Este guard evita un refactor silencioso que lo restaure.
        var method = typeof(IUsuarioApiClient).GetMethod("GetRolesAsync");

        Assert.Null(method);
    }

    [Fact]
    public void Interface_ExposesExactlyTenPublicAsyncMethods()
    {
        // Defensa contra refactors "creativos" que sumen un nuevo método
        // (e.g. <c>BulkCreateAsync</c>) sin actualizar la suite de
        // contract tests. Tras la baja de ReactivarAsync y la baja del
        // alias DeleteAsync (Phase 3 del change
        // 2026-07-15-quita-soft-delete-usuario) quedan 9 métodos
        // "primary" (GetAllActivasAsync, QueryAsync, GetByIdAsync,
        // CreateAsync, UpdateAsync, EliminarAsync, BloquearAsync,
        // DesbloquearAsync, AssignRolesAsync) más el alias
        // <c>DeleteAsync</c> default-implemented, total 10. En realidad
        // AssignRolesAsync no está en IUsuarioApiClient (lo cubre
        // PersonaApiClient), así que quedan exactamente los 9 primarios
        // de Web + DeleteAsync = 10.
        var publicMethods = typeof(IUsuarioApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName) // excluye accessors
            .Select(m => m.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "BloquearAsync",
                "CreateAsync",
                "DeleteAsync",
                "DesbloquearAsync",
                "EliminarAsync",
                "GetAllActivasAsync",
                "GetByIdAsync",
                "QueryAsync",
                "UpdateAsync"
            },
            publicMethods);
    }
}
