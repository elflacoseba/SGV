using System.Reflection;
using System.Text.Json;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Aplicacion.Seguridad;

public sealed class UsuarioContractsTests
{
    [Fact]
    public void UsuarioDto_AppendsNullablePersonaNamesAndLockoutFlagAfterExistingProperties()
    {
        var constructor = Assert.Single(typeof(UsuarioDto).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Equal(
            ["Id", "PersonaId", "UserName", "Email", "Roles", "Nombres", "Apellidos", "Bloqueado"],
            parameters.Select(parameter => parameter.Name!).ToArray());
        Assert.Equal(typeof(string), parameters[5].ParameterType);
        Assert.Equal(typeof(string), parameters[6].ParameterType);
        Assert.True(new NullabilityInfoContext().Create(parameters[5]).ReadState is NullabilityState.Nullable);
        Assert.True(new NullabilityInfoContext().Create(parameters[6]).ReadState is NullabilityState.Nullable);
        Assert.Equal(typeof(bool), parameters[7].ParameterType);
        Assert.True(parameters[7].HasDefaultValue);
    }

    [Fact]
    public void UsuarioListQuery_DefaultsToActiveSegment()
    {
        var query = new UsuarioListQuery(2, 25, "ana", "apellidos_desc");

        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal("ana", query.Search);
        Assert.Equal("apellidos_desc", query.Sort);
        Assert.Equal(UsuarioSegmentoListado.Activas, query.Segmento);
    }

    [Fact]
    public void UsuarioListadoDto_WrapsPagedResultWithoutChangingPaginationMetadata()
    {
        var page = new PagedResult<UsuarioDto>(
            [new UsuarioDto("user-1", Guid.NewGuid(), "admin", "admin@test.com", ["Administrador"], "Ana", "Pérez")],
            31,
            2,
            20);

        var result = new UsuarioListadoDto(page);

        Assert.Same(page, result.Result);
        Assert.Equal(31, result.Result.TotalCount);
        Assert.Equal(2, result.Result.Page);
        Assert.Equal(20, result.Result.PageSize);
    }

    [Fact]
    public void ActualizarUsuarioRequest_CarriesCredentialsAndRoleSetAtomically()
    {
        var request = new ActualizarUsuarioRequest("new-name", "new@test.com", ["Consultor"]);

        Assert.Equal("new-name", request.UserName);
        Assert.Equal("new@test.com", request.Email);
        Assert.Equal(["Consultor"], request.Roles);
    }

    // ──────────────────────────────────────────────────────────────
    // PR2-HALL-1: extender `UsuarioCommandResult` con FieldErrors.
    // Espejo del shape canónico usado por
    // CargoCommandResult / PuestoCommandResult /
    // UnidadOrganizativaCommandResult / HabilidadCommandResult /
    // PersonaCommandResult: `IReadOnlyDictionary<string, string[]>?`
    // con default null y dos factories overload (con y sin
    // fieldErrors). Tests RED — la propiedad no existe todavía en
    // el record del PR1.
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void UsuarioCommandResult_FailureWithFieldErrors_StoresDictionaryAndPreservesErrorCategoria()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["userName"] = new[] { "El nombre de usuario ya está en uso." },
            ["personaId"] = new[] { "Debe seleccionar una persona activa." }
        };
        var error = new UsuarioError(
            UsuarioErrorType.Validation,
            "ValidationError",
            "Datos inválidos.",
            StatusCode: 400,
            Categoria: ErrorCategoria.Validation);

        var result = UsuarioCommandResult.Failure(error, fieldErrors);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FieldErrors);
        Assert.Equal(2, result.FieldErrors!.Count);
        Assert.Equal("El nombre de usuario ya está en uso.", result.FieldErrors["userName"][0]);
        Assert.Equal("Debe seleccionar una persona activa.", result.FieldErrors["personaId"][0]);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
    }

    [Fact]
    public void UsuarioCommandResult_FailureWithoutFieldErrors_StoresEmptyDictionaryByDefault()
    {
        // El factory simple `Failure(error)` debe seguir siendo
        // source-compatible con los call sites existentes del PR2 (el
        // `UsuarioApiClient` lo invoca cuando la respuesta no es
        // ValidationProblemDetails). Por defecto, `FieldErrors` queda
        // null para que la rama de "no per-field errors" sea trivial
        // de detectar con `is null` o `Count == 0`.
        var error = new UsuarioError(
            UsuarioErrorType.Conflict,
            "UserNameDuplicado",
            "Ya existe un usuario activo con el mismo UserName.");

        var result = UsuarioCommandResult.Failure(error);

        Assert.False(result.IsSuccess);
        // Failure(error) sin fieldErrors ⇒ null (mismo trato que
        // CargoCommandResult/PuestoCommandResult/etc.).
        Assert.Null(result.FieldErrors);
        Assert.Equal("UserNameDuplicado", result.Error!.Code);
    }

    [Fact]
    public void UsuarioCommandResult_Success_DoesNotExposeFieldErrors()
    {
        var dto = new UsuarioDto(
            "u-1", Guid.NewGuid(), "agarcía", "agarcia@example.com",
            new[] { "Administrador" });

        var result = UsuarioCommandResult.Success(dto);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Same(dto, result.Value);
        // En Success no aplica — la propiedad queda null.
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public void UsuarioCommandResult_FailureWithNullFieldErrors_DoesNotThrow()
    {
        var error = new UsuarioError(UsuarioErrorType.NotFound, "NotFound", "Recurso no encontrado.");

        var exception = Record.Exception(() => UsuarioCommandResult.Failure(error, fieldErrors: null));

        Assert.Null(exception);
    }

    [Fact]
    public void UsuarioCommandResult_FailureWithEmptyFieldErrors_StoresEmptyDictionary()
    {
        // El caso `ValidationProblemDetails` con clave `errors` pero
        // sin entradas: el `ApiProblemReader` lo materializa como
        // `Dictionary<string, string[]>(StringComparer.Ordinal)`
        // vacío. El factory debe aceptarlo y preservarlo tal cual
        // (clave vacía ≠ null) para que el mapper distinga
        // "shape Validation sin per-field" de "shape ProblemDetails
        // plano".
        var error = new UsuarioError(UsuarioErrorType.Validation, "ValidationError", "Datos inválidos.",
            StatusCode: 400, Categoria: ErrorCategoria.Validation);
        var fieldErrors = new Dictionary<string, string[]>();

        var result = UsuarioCommandResult.Failure(error, fieldErrors);

        Assert.NotNull(result.FieldErrors);
        Assert.Empty(result.FieldErrors!);
    }

    [Fact]
    public void UsuarioCommandResult_FailureWithFieldErrors_RoundTripsThroughSystemTextJson()
    {
        // AC: el wire del backend entrega `ValidationProblemDetails`
        // con clave `errors` mapeada a `Dictionary<string, string[]>`.
        // El record debe poder (des)serializar vía System.Text.Json
        // preservando la forma del diccionario (incluyendo el casing
        // de la clave `FieldErrors` en camelCase) para que el shell
        // web pueda recibir el CommandResult serializado si en el
        // futuro la API elige devolverlo en lugar de
        // ValidationProblemDetails.
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["userName"] = new[] { "ya está en uso" }
        };
        var error = new UsuarioError(
            UsuarioErrorType.Validation,
            "ValidationError",
            "Datos inválidos.",
            StatusCode: 400,
            Categoria: ErrorCategoria.Validation);
        var original = UsuarioCommandResult.Failure(error, fieldErrors);

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<UsuarioCommandResult>(json);

        Assert.NotNull(roundTripped);
        Assert.False(roundTripped!.IsSuccess);
        Assert.Equal("ValidationError", roundTripped.Error!.Code);
        Assert.Equal(ErrorCategoria.Validation, roundTripped.Error.Categoria);
        Assert.NotNull(roundTripped.FieldErrors);
        Assert.Equal("ya está en uso", roundTripped.FieldErrors!["userName"][0]);
    }

    [Fact]
    public void UsuarioError_ValidationCategoriaCoexistsWithFieldErrors()
    {
        // El mapper común (`CommandResultMapper.Map`) ya produce
        // `ErrorCategoria.Validation` cuando el status HTTP es 400 o
        // 422. La PageModel del PR4 ramificará por `Categoria`
        // ANTES de inspeccionar FieldErrors; este test garantiza que
        // ambos miembros coexisten en la shape canónica.
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["email"] = new[] { "El email no tiene un formato válido." }
        };
        var result = UsuarioCommandResult.Failure(
            new UsuarioError(UsuarioErrorType.Validation, "ValidationError", "Datos inválidos.",
                StatusCode: 400, Categoria: ErrorCategoria.Validation),
            fieldErrors);

        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
        Assert.Equal(UsuarioErrorType.Validation, result.Error.Type);
        Assert.Contains("email", result.FieldErrors!.Keys);
    }
}
