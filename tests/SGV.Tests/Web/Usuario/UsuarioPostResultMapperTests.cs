using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Usuarios;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Tests de seam para <see cref="UsuarioPostResultMapper"/>.
/// </summary>
/// <remarks>
/// <para>
/// PR2-HALL: el mapper actual no soporta FieldErrors porque
/// <see cref="UsuarioCommandResult"/> (shape heredado del PR1) no
/// expone ese miembro. Ver <see cref="UsuarioPostResultMapper"/> para
/// la justificación; el gap se cierra en PR 3/4 cuando el contrato
/// amplíe el record y el mapper sume la rama FieldErrors. Esta suite
/// verifica el shape vigente y deja un test marcado como
/// <c>Skip = …</c> si más adelante se quiere triangular la rama
/// FieldErrors una vez agregado el miembro al record.
/// </para>
/// <para>
/// El mapper siempre devuelve <c>false</c> en PR 2 (no hay rama
/// FieldErrors); los tests del success/null/message-only cubren el
/// comportamiento real.
/// </para>
/// </remarks>
public class UsuarioPostResultMapperTests
{
    [Fact]
    public void TryMap_WithNullResult_ReturnsFalseAndDoesNotMutateModelState()
    {
        var modelState = new ModelStateDictionary();

        var handled = UsuarioPostResultMapper.TryMap(null, modelState);

        Assert.False(handled);
        Assert.Empty(modelState);
    }

    [Fact]
    public void TryMap_WithSuccessResult_ReturnsFalseAndDoesNotMutateModelState()
    {
        var dto = new UsuarioDto(
            "u-1", Guid.NewGuid(), "agarcía", "agarcia@example.com",
            new[] { "Administrador" });
        var success = UsuarioCommandResult.Success(dto);
        var modelState = new ModelStateDictionary();

        var handled = UsuarioPostResultMapper.TryMap(success, modelState);

        Assert.False(handled);
        Assert.Empty(modelState);
    }

    [Fact]
    public void TryMap_WithFailureWithMessage_AddsSummaryErrorAndReturnsFalse()
    {
        var failure = UsuarioCommandResult.Failure(
            new UsuarioError(UsuarioErrorType.NotFound, "PersonaNoEncontrada", "La persona vinculada no existe."));
        var modelState = new ModelStateDictionary();

        var handled = UsuarioPostResultMapper.TryMap(failure, modelState);

        Assert.False(handled);
        Assert.Single(modelState);
        Assert.Equal("La persona vinculada no existe.", modelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public void TryMap_WithFailureEmptyMessage_DoesNotMutateModelStateAndReturnsFalse()
    {
        var failure = UsuarioCommandResult.Failure(
            new UsuarioError(UsuarioErrorType.Conflict, "UserNameDuplicado", string.Empty));
        var modelState = new ModelStateDictionary();

        var handled = UsuarioPostResultMapper.TryMap(failure, modelState);

        Assert.False(handled);
        // No se agrega nada al ModelState porque el mensaje está
        // vacío; el PageModel decide cómo reaccionar.
        Assert.Empty(modelState);
    }

    [Fact]
    public void TryMap_WithFieldErrors_AppliesPerFieldPrefixAndReturnsTrue()
    {
        // PR2-HALL-1: el mapper debe cerrar la brecha y propagar
        // FieldErrors al ModelState bajo el prefijo "Input." para
        // que las tag helpers `asp-validation-for` rendericen el
        // mensaje junto al campo correspondiente (Create/Edit en
        // PR 4). Espejo del `CargoPostResultMapper.TryMap_FieldErrors…`
        // y del `PuestoPostResultMapper.TryMapCommandResult_FieldErrors…`.
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["userName"] = new[] { "El nombre de usuario ya está en uso." },
            ["personaId"] = new[] { "Debe seleccionar una persona activa." }
        };
        var failure = UsuarioCommandResult.Failure(
            new UsuarioError(UsuarioErrorType.Validation, "ValidationError", "Datos inválidos.",
                StatusCode: 400, Categoria: ErrorCategoria.Validation),
            fieldErrors);
        var modelState = new ModelStateDictionary();

        var handled = UsuarioPostResultMapper.TryMap(failure, modelState);

        Assert.True(handled);
        Assert.True(modelState.ContainsKey($"{UsuarioFormKeys.InputPrefix}userName"));
        Assert.Equal(
            "El nombre de usuario ya está en uso.",
            modelState[$"{UsuarioFormKeys.InputPrefix}userName"]!.Errors[0].ErrorMessage);
        Assert.True(modelState.ContainsKey($"{UsuarioFormKeys.InputPrefix}personaId"));
        Assert.Equal(
            "Debe seleccionar una persona activa.",
            modelState[$"{UsuarioFormKeys.InputPrefix}personaId"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public void TryMap_WithEmptyFieldErrorsDictionary_FallsThroughToErrorMessageAndReturnsFalse()
    {
        // Distinción clave: un `FieldErrors` no-nulo pero vacío NO
        // se considera "campo aplicado". El mapper debe caer al
        // fallback del `Error.Message` (bajo la clave vacía) y
        // devolver `false`. Mismo trato que
        // `CargoPostResultMapper.TryMap_EmptyFieldErrorsDictionary_FallsThroughToErrorMessage`.
        var failure = UsuarioCommandResult.Failure(
            new UsuarioError(UsuarioErrorType.Conflict, "UserNameDuplicado", "Duplicado.",
                StatusCode: 409, Categoria: ErrorCategoria.Conflict),
            new Dictionary<string, string[]>());
        var modelState = new ModelStateDictionary();

        var handled = UsuarioPostResultMapper.TryMap(failure, modelState);

        Assert.False(handled);
        Assert.True(modelState.ContainsKey(string.Empty));
        Assert.Equal("Duplicado.", modelState[string.Empty]!.Errors[0].ErrorMessage);
    }
}
