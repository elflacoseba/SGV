using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Unit tests for <see cref="CargoPostResultMapper"/>. Covers the four
/// outcomes of <c>TryMap</c>: null result, empty result, result with
/// field-level errors, and result with a single general error message.
/// </summary>
public sealed class CargoPostResultMapperTests
{
    [Fact]
    public void TryMap_NullResult_ReturnsFalseAndLeavesModelStateUntouched()
    {
        var modelState = new ModelStateDictionary();

        var mapped = CargoPostResultMapper.TryMap(null, modelState);

        Assert.False(mapped);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void TryMap_EmptyFailureResult_ReturnsFalseAndLeavesModelStateUntouched()
    {
        var modelState = new ModelStateDictionary();
        var result = new CargoCommandResult(
            IsSuccess: false,
            Value: null,
            Error: null,
            FieldErrors: null);

        var mapped = CargoPostResultMapper.TryMap(result, modelState);

        Assert.False(mapped);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void TryMap_SuccessResult_ReturnsFalseAndLeavesModelStateUntouched()
    {
        var modelState = new ModelStateDictionary();
        var result = CargoCommandResult.Success(
            new CargoDto(Guid.NewGuid(), "C-001", "Analista", null, Guid.NewGuid()));

        var mapped = CargoPostResultMapper.TryMap(result, modelState);

        Assert.False(mapped);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void TryMap_FieldErrorsWithMultipleKeysAndMessages_AppliesAllToModelStateAndReturnsTrue()
    {
        var modelState = new ModelStateDictionary();
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "ya existe" },
            ["nombre"] = new[] { "es obligatorio", "máx 200" }
        };
        var result = new CargoCommandResult(
            IsSuccess: false,
            Value: null,
            Error: new CargoError(CargoErrorType.Validation, "Validation", "validation failed"),
            FieldErrors: fieldErrors);

        var mapped = CargoPostResultMapper.TryMap(result, modelState);

        Assert.True(mapped);
        Assert.True(modelState.ContainsKey($"{CargoFormKeys.InputPrefix}codigo"));
        var codigoErrors = modelState[$"{CargoFormKeys.InputPrefix}codigo"]!.Errors;
        Assert.Single(codigoErrors);
        Assert.Equal("ya existe", codigoErrors[0].ErrorMessage);

        Assert.True(modelState.ContainsKey($"{CargoFormKeys.InputPrefix}nombre"));
        var nombreErrors = modelState[$"{CargoFormKeys.InputPrefix}nombre"]!.Errors;
        Assert.Equal(2, nombreErrors.Count);
        Assert.Equal("es obligatorio", nombreErrors[0].ErrorMessage);
        Assert.Equal("máx 200", nombreErrors[1].ErrorMessage);
    }

    [Fact]
    public void TryMap_ErrorMessageWithoutFieldErrors_AppliesUnderEmptyKeyAndReturnsFalse()
    {
        var modelState = new ModelStateDictionary();
        var result = new CargoCommandResult(
            IsSuccess: false,
            Value: null,
            Error: new CargoError(CargoErrorType.NotFound, "NotFound", "Recurso no encontrado."),
            FieldErrors: null);

        var mapped = CargoPostResultMapper.TryMap(result, modelState);

        Assert.False(mapped);
        Assert.True(modelState.ContainsKey(string.Empty));
        var summaryErrors = modelState[string.Empty]!.Errors;
        Assert.Single(summaryErrors);
        Assert.Equal("Recurso no encontrado.", summaryErrors[0].ErrorMessage);
    }

    [Fact]
    public void TryMap_EmptyFieldErrorsDictionary_FallsThroughToErrorMessage()
    {
        var modelState = new ModelStateDictionary();
        var result = new CargoCommandResult(
            IsSuccess: false,
            Value: null,
            Error: new CargoError(CargoErrorType.Conflict, "Conflict", "Conflicto."),
            FieldErrors: new Dictionary<string, string[]>());

        var mapped = CargoPostResultMapper.TryMap(result, modelState);

        Assert.False(mapped);
        Assert.True(modelState.ContainsKey(string.Empty));
        Assert.Equal("Conflicto.", modelState[string.Empty]!.Errors[0].ErrorMessage);
    }
}
