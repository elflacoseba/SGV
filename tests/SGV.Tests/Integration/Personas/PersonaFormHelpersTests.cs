using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Integration.Personas;

/// <summary>
/// Comportamiento observable de <see cref="PersonaFormHelpers.ApplyFieldErrorsToModelState"/>.
/// Garantiza que la conversión entre <c>ValidationProblemDetails.Errors</c>
/// (camelCase) y las claves <c>asp-validation-for="Input.Xyz"</c> del
/// formulario Razor preserva la asociación campo → mensaje para que el
/// usuario vea el error al lado del control correcto.
/// </summary>
public class PersonaFormHelpersTests
{
    [Fact]
    public void ApplyFieldErrorsToModelState_WithNullDictionary_DoesNotAddAnyError()
    {
        var modelState = new ModelStateDictionary();

        PersonaFormHelpers.ApplyFieldErrorsToModelState(modelState, fieldErrors: null);

        Assert.Equal(0, modelState.ErrorCount);
        Assert.True(modelState.IsValid);
    }

    [Fact]
    public void ApplyFieldErrorsToModelState_WithSingleField_PrefixesKeyWithInputDot()
    {
        var modelState = new ModelStateDictionary();
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["legajo"] = new[] { "El legajo es obligatorio." }
        };

        PersonaFormHelpers.ApplyFieldErrorsToModelState(modelState, fieldErrors);

        Assert.True(modelState.ContainsKey("Input.Legajo"));
        var error = modelState["Input.Legajo"]!.Errors.Single();
        Assert.Equal("El legajo es obligatorio.", error.ErrorMessage);
    }

    [Fact]
    public void ApplyFieldErrorsToModelState_WithMultipleFieldsAndMessages_AddsEveryKeyValuePair()
    {
        // Triangulación: simula respuesta 400 con ValidationProblemDetails real
        // para legajo, email y numeroDocumento. Cada mensaje debe mapearse a
        // su clave exacta prefijada con "Input." para que asp-validation-for
        // los encuentre en los controles correctos.
        var modelState = new ModelStateDictionary();
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["legajo"] = new[] { "El legajo es obligatorio.", "Máximo 20 caracteres." },
            ["email"] = new[] { "Email inválido." },
            ["numeroDocumento"] = new[] { "Ya existe una persona activa con este documento." }
        };

        PersonaFormHelpers.ApplyFieldErrorsToModelState(modelState, fieldErrors);

        Assert.True(modelState.ContainsKey("Input.Legajo"));
        Assert.True(modelState.ContainsKey("Input.Email"));
        Assert.True(modelState.ContainsKey("Input.NumeroDocumento"));
        Assert.Equal(2, modelState["Input.Legajo"]!.Errors.Count);
        Assert.Equal("Email inválido.", modelState["Input.Email"]!.Errors.Single().ErrorMessage);
        Assert.Equal("Ya existe una persona activa con este documento.",
            modelState["Input.NumeroDocumento"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void ApplyFieldErrorsToModelState_PrefixMatchesPersonaFormKeysInputPrefix()
    {
        // REQ-4.2 (tasks.md): el prefix debe ser estable y debe matchear el
        // que PersonaFormKeys declara. Si el helper cambiara el prefix sin
        // actualizar la constante, las claves quedarían huérfanas y los
        // asp-validation-for del _Form.cshtml no los renderizarían.
        var modelState = new ModelStateDictionary();
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["apellidos"] = new[] { "Los apellidos son obligatorios." }
        };

        PersonaFormHelpers.ApplyFieldErrorsToModelState(modelState, fieldErrors);

        Assert.True(modelState.ContainsKey(PersonaFormKeys.InputPrefix + "Apellidos"));
    }
}
