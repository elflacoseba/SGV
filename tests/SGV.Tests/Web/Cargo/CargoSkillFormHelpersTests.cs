using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Web.Pages.Organizacion.Cargos;
using Xunit;

namespace SGV.Tests.Web.Cargo;

public sealed class CargoSkillFormHelpersTests
{
    [Fact]
    public void ReadAsignarInput_ValidForm_MapsValuesAndKeepsModelStateValid()
    {
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var modelState = new ModelStateDictionary();
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["AsignarInput.SkillId"] = skillId.ToString(),
            ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
            ["AsignarInput.Ponderacion"] = "2.50",
            ["AsignarInput.EsObligatoria"] = "true"
        });

        var input = CargoSkillFormHelpers.ReadAsignarInput(form, modelState);

        Assert.True(modelState.IsValid);
        Assert.Equal(skillId, input.SkillId);
        Assert.Equal(nivelId, input.NivelRequeridoId);
        Assert.Equal(2.50m, input.Ponderacion);
        Assert.True(input.EsObligatoria);
    }

    [Fact]
    public void TryReadActualizarRequest_InvalidPonderacion_AddsRowScopedErrorAndDoesNotCreateRequest()
    {
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var modelState = new ModelStateDictionary();
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            [$"Actualizar[{skillId}].NivelRequeridoId"] = nivelId.ToString(),
            [$"Actualizar[{skillId}].Ponderacion"] = "999",
            [$"Actualizar[{skillId}].EsObligatoria"] = "false"
        });

        var success = CargoSkillFormHelpers.TryReadActualizarRequest(skillId, form, modelState, out var request);

        Assert.False(success);
        Assert.Null(request);
        Assert.False(modelState.IsValid);
        Assert.True(modelState.ContainsKey($"Actualizar[{skillId}].Ponderacion"));
    }

    [Fact]
    public void ApplyActualizarFailureToModelState_WhitelistedField_AnchorsToRowAndSummary()
    {
        var skillId = Guid.NewGuid();
        var modelState = new ModelStateDictionary();
        var result = CargoSkillCommandResult.Failure(
            new CargoSkillError(CargoSkillErrorType.Validation, "DatosInvalidos", "Invalid."),
            new Dictionary<string, string[]>
            {
                ["Ponderacion"] = ["Fuera de rango"]
            });

        CargoSkillFormHelpers.ApplyActualizarFailureToModelState(skillId, result, modelState);

        Assert.True(modelState.ContainsKey($"Actualizar[{skillId}].Ponderacion"));
        Assert.True(modelState.ContainsKey(string.Empty));
        Assert.Contains(modelState[$"Actualizar[{skillId}].Ponderacion"]!.Errors, error => error.ErrorMessage == "Fuera de rango");
        Assert.Contains(modelState[string.Empty]!.Errors, error => error.ErrorMessage == "Fuera de rango");
    }

    [Fact]
    public void ApplyAsignarFailureToModelState_BackendFieldError_PrefixesAsignarInputKey()
    {
        var modelState = new ModelStateDictionary();
        var result = CargoSkillCommandResult.Failure(
            new CargoSkillError(CargoSkillErrorType.Validation, "DatosInvalidos", "Invalid."),
            new Dictionary<string, string[]>
            {
                ["Ponderacion"] = ["La ponderación no puede superar 100.00."]
            });

        CargoSkillFormHelpers.ApplyAsignarFailureToModelState(result, modelState);

        Assert.True(modelState.ContainsKey("AsignarInput.Ponderacion"));
        Assert.Contains(modelState["AsignarInput.Ponderacion"]!.Errors, error => error.ErrorMessage.Contains("ponderación", StringComparison.OrdinalIgnoreCase));
    }
}
