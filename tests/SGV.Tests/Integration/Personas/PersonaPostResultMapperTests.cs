using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Integration.Personas;

/// <summary>
/// Comportamiento observable de <see cref="PersonaPostResultMapper.TryMap"/>.
/// Cubre las tres rutas que un PageModel de Create/Edit de Personas debe
/// tomar tras un POST: éxito, fallos con FieldErrors (400) y fallos sin
/// FieldErrors (404/409/etc.).
/// </summary>
public class PersonaPostResultMapperTests
{
    [Fact]
    public void TryMap_WithNullResult_ReturnsFalseAndDoesNotTouchModelState()
    {
        var modelState = new ModelStateDictionary();

        var applied = PersonaPostResultMapper.TryMap(result: null, modelState);

        Assert.False(applied);
        Assert.True(modelState.IsValid);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void TryMap_WithSuccessResult_ReturnsFalseWithoutAddingModelError()
    {
        // REQ: un éxito no debe contaminar ModelState. Si lo hiciera,
        // el asp-validation-summary="ModelOnly" del Edit.cshtml podría
        // mostrar errores residuales cuando hay TempData success.
        var dto = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", "ana@example.com", null, null, "DNI", "30123456", "+549111234", true);
        var success = PersonaCommandResult.Success(dto);
        var modelState = new ModelStateDictionary();

        var applied = PersonaPostResultMapper.TryMap(success, modelState);

        Assert.False(applied);
        Assert.True(modelState.IsValid);
    }

    [Fact]
    public void TryMap_WithFailureAndFieldErrors_AppliesPerFieldPrefixAndReturnsTrue()
    {
        // Camino crítico del flujo Create/Edit: 400 con ValidationProblemDetails
        // ⇒ entradas de per-field deben ir a ModelState["Input.X"] para que
        // los asp-validation-for del _Form.cshtml los rendericen pegados al
        // control correspondiente. El mapper debe devolver true para que el
        // caller sepa que se aplicaron FieldErrors (y no muestre el error
        // general del success fallback).
        var error = new PersonaError(
            PersonaErrorType.Validation, "ValidationError", "Datos inválidos.",
            StatusCode: 400, Categoria: SGV.Contracts.Comun.ErrorCategoria.Validation);
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["legajo"] = new[] { "El legajo es obligatorio." },
            ["email"] = new[] { "Email inválido." }
        };
        var failure = PersonaCommandResult.Failure(error, fieldErrors);
        var modelState = new ModelStateDictionary();

        var applied = PersonaPostResultMapper.TryMap(failure, modelState);

        Assert.True(applied);
        Assert.True(modelState.ContainsKey("Input.Legajo"));
        Assert.True(modelState.ContainsKey("Input.Email"));
        Assert.False(modelState.ContainsKey(string.Empty));
    }

    [Fact]
    public void TryMap_WithFailureAndErrorMessageOnly_AddsGeneralModelError()
    {
        // Camino 409 sin FieldErrors: el mapper debe poner el mensaje bajo la
        // clave vacía para que asp-validation-summary="ModelOnly" lo muestre,
        // y debe devolver false para que el caller aplique el feedback
        // correspondiente (TempData["Error"] por ejemplo).
        var error = new PersonaError(
            PersonaErrorType.Conflict, "LegajoDuplicado",
            "Ya existe una persona activa con el legajo L-001.",
            StatusCode: 409, Categoria: SGV.Contracts.Comun.ErrorCategoria.Conflict);
        var failure = PersonaCommandResult.Failure(error);
        var modelState = new ModelStateDictionary();

        var applied = PersonaPostResultMapper.TryMap(failure, modelState);

        Assert.False(applied);
        Assert.True(modelState.ContainsKey(string.Empty));
        Assert.Equal(
            "Ya existe una persona activa con el legajo L-001.",
            modelState[string.Empty]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void TryMap_WithFailureAndEmptyErrorMessage_LeavesModelStateUntouched()
    {
        // Edge case: backend devolvió 400 con Failure pero sin mensaje (no
        // debería ocurrir pero el mapper debe degradar sin agregar ruido).
        var error = new PersonaError(
            PersonaErrorType.Validation, "ValidationError",
            Message: string.Empty, StatusCode: 400,
            Categoria: SGV.Contracts.Comun.ErrorCategoria.Validation);
        var failure = PersonaCommandResult.Failure(error);
        var modelState = new ModelStateDictionary();

        var applied = PersonaPostResultMapper.TryMap(failure, modelState);

        Assert.False(applied);
        Assert.True(modelState.IsValid);
    }
}
