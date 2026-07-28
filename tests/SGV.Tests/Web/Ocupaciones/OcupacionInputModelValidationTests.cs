using System.ComponentModel.DataAnnotations;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Web.Integration.Ocupaciones;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests RED→GREEN de validación declarativa del <see cref="OcupacionInputModel"/>
/// del módulo web de Ocupaciones (Slice 3a del change #208). Validan el
/// contrato cliente+servidor: <c>[Required]</c> en PersonaId/PuestoId/
/// FechaInicio/TipoAsignacion, <c>[StringLength(500)]</c> en Observaciones.
/// </summary>
/// <remarks>
/// Los tests ejercitan <see cref="Validator.TryValidateObject"/> directamente
/// (sin ASP.NET hosting) porque la matriz de atributos
/// <see cref="ValidationAttribute"/> es 100% portable. La cobertura del
/// flujo de PageModel completo vive en <c>OcupacionCreatePageTests</c>
/// (ServerValidator: el binding real contra el POST).
/// </remarks>
public sealed class OcupacionInputModelValidationTests
{
    private static List<ValidationResult> Validate(OcupacionInputModel model)
    {
        var ctx = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    private static OcupacionInputModel ValidModel() => new()
    {
        PersonaId = Guid.NewGuid(),
        PuestoId = Guid.NewGuid(),
        FechaInicio = new DateOnly(2026, 1, 15),
        TipoAsignacion = OcupacionTipoAsignacion.Permanente,
        Observaciones = null
    };

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-004 / Scenario: Alta válida sin observaciones
    // ──────────────────────────────────────────────────

    [Fact]
    public void Validate_AllRequiredFieldsPresent_NoErrors()
    {
        var model = ValidModel();

        var results = Validate(model);

        Assert.Empty(results);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-004 / Scenario: campos requeridos faltantes
    // ──────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(OcupacionInputModel.PersonaId))]
    [InlineData(nameof(OcupacionInputModel.PuestoId))]
    [InlineData(nameof(OcupacionInputModel.FechaInicio))]
    [InlineData(nameof(OcupacionInputModel.TipoAsignacion))]
    public void Validate_RequiredFieldMissing_EmitsFieldError(string missingField)
    {
        var model = ValidModel();
        switch (missingField)
        {
            case nameof(OcupacionInputModel.PersonaId): model.PersonaId = null; break;
            case nameof(OcupacionInputModel.PuestoId): model.PuestoId = null; break;
            case nameof(OcupacionInputModel.FechaInicio): model.FechaInicio = null; break;
            case nameof(OcupacionInputModel.TipoAsignacion): model.TipoAsignacion = null; break;
        }

        var results = Validate(model);

        var memberNames = results.SelectMany(r => r.MemberNames).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(missingField, memberNames);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-004 / Scenario: Observaciones > 500 caracteres
    // ──────────────────────────────────────────────────

    [Fact]
    public void Validate_ObservacionesExceedsMaxLength_EmitsStringLengthError()
    {
        var model = ValidModel();
        model.Observaciones = new string('x', 501);

        var results = Validate(model);

        var memberNames = results.SelectMany(r => r.MemberNames).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(nameof(OcupacionInputModel.Observaciones), memberNames);
    }

    [Fact]
    public void Validate_ObservacionesAtMaxLength_NoErrors()
    {
        var model = ValidModel();
        model.Observaciones = new string('x', 500);

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_ObservacionesBelowMaxLength_NoErrors()
    {
        var model = ValidModel();
        model.Observaciones = "Observación corta";

        var results = Validate(model);

        Assert.Empty(results);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-004 / Scenario: TipoAsignacion acepta los 3 valores
    // ──────────────────────────────────────────────────

    [Theory]
    [InlineData(OcupacionTipoAsignacion.Permanente)]
    [InlineData(OcupacionTipoAsignacion.Interina)]
    [InlineData(OcupacionTipoAsignacion.Temporal)]
    public void Validate_AllTipoAsignacionValues_AreAccepted(OcupacionTipoAsignacion tipo)
    {
        var model = ValidModel();
        model.TipoAsignacion = tipo;

        var results = Validate(model);

        Assert.Empty(results);
    }
}