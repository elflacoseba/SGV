using Microsoft.AspNetCore.Mvc.ModelBinding;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Unit tests for <see cref="PuestoPostResultMapper"/>. Cubre los cuatro
/// outcomes de <c>TryMapCommandResult</c>: null result, success result,
/// result con field-level errors y result con mensaje general. Espejo de
/// <c>CargoPostResultMapperTests</c>.
/// </summary>
public sealed class PuestoPostResultMapperTests
{
    [Fact]
    public void TryMapCommandResult_NullResult_ReturnsFalseAndLeavesModelStateUntouched()
    {
        var modelState = new ModelStateDictionary();

        var mapped = PuestoPostResultMapper.TryMapCommandResult(null, modelState);

        Assert.False(mapped);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void TryMapCommandResult_EmptyFailureResult_ReturnsFalseAndLeavesModelStateUntouched()
    {
        var modelState = new ModelStateDictionary();
        var result = new PuestoCommandResult(
            IsSuccess: false,
            Value: null,
            Error: null,
            FieldErrors: null);

        var mapped = PuestoPostResultMapper.TryMapCommandResult(result, modelState);

        Assert.False(mapped);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void TryMapCommandResult_SuccessResult_ReturnsFalseAndLeavesModelStateUntouched()
    {
        var modelState = new ModelStateDictionary();
        var result = PuestoCommandResult.Success(
            new PuestoDto(
                Guid.NewGuid(),
                "P-001",
                "Director",
                null,
                Guid.NewGuid(),
                "Comercial",
                Guid.NewGuid(),
                "Vendedor",
                null));

        var mapped = PuestoPostResultMapper.TryMapCommandResult(result, modelState);

        Assert.False(mapped);
        Assert.Equal(0, modelState.ErrorCount);
    }

    [Fact]
    public void TryMapCommandResult_FieldErrorsWithMultipleKeysAndMessages_AppliesAllToModelStateAndReturnsTrue()
    {
        var modelState = new ModelStateDictionary();
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "ya existe" },
            ["nombre"] = new[] { "es obligatorio", "máx 200" }
        };
        var result = new PuestoCommandResult(
            IsSuccess: false,
            Value: null,
            Error: new PuestoError(PuestoErrorType.Validation, "Validation", "validation failed"),
            FieldErrors: fieldErrors);

        var mapped = PuestoPostResultMapper.TryMapCommandResult(result, modelState);

        Assert.True(mapped);
        Assert.True(modelState.ContainsKey($"{PuestoFormKeys.InputPrefix}codigo"));
        var codigoErrors = modelState[$"{PuestoFormKeys.InputPrefix}codigo"]!.Errors;
        Assert.Single(codigoErrors);
        Assert.Equal("ya existe", codigoErrors[0].ErrorMessage);

        Assert.True(modelState.ContainsKey($"{PuestoFormKeys.InputPrefix}nombre"));
        var nombreErrors = modelState[$"{PuestoFormKeys.InputPrefix}nombre"]!.Errors;
        Assert.Equal(2, nombreErrors.Count);
        Assert.Equal("es obligatorio", nombreErrors[0].ErrorMessage);
        Assert.Equal("máx 200", nombreErrors[1].ErrorMessage);
    }

    [Fact]
    public void TryMapCommandResult_ErrorMessageWithoutFieldErrors_AppliesUnderEmptyKeyAndReturnsFalse()
    {
        var modelState = new ModelStateDictionary();
        var result = new PuestoCommandResult(
            IsSuccess: false,
            Value: null,
            Error: new PuestoError(PuestoErrorType.NotFound, "NotFound", "Recurso no encontrado."),
            FieldErrors: null);

        var mapped = PuestoPostResultMapper.TryMapCommandResult(result, modelState);

        Assert.False(mapped);
        Assert.True(modelState.ContainsKey(string.Empty));
        var summaryErrors = modelState[string.Empty]!.Errors;
        Assert.Single(summaryErrors);
        Assert.Equal("Recurso no encontrado.", summaryErrors[0].ErrorMessage);
    }

    [Fact]
    public void TryMapCommandResult_EmptyFieldErrorsDictionary_FallsThroughToErrorMessage()
    {
        var modelState = new ModelStateDictionary();
        var result = new PuestoCommandResult(
            IsSuccess: false,
            Value: null,
            Error: new PuestoError(PuestoErrorType.Conflict, "Conflict", "Conflicto."),
            FieldErrors: new Dictionary<string, string[]>());

        var mapped = PuestoPostResultMapper.TryMapCommandResult(result, modelState);

        Assert.False(mapped);
        Assert.True(modelState.ContainsKey(string.Empty));
        Assert.Equal("Conflicto.", modelState[string.Empty]!.Errors[0].ErrorMessage);
    }
}
