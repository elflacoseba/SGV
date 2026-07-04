using FluentValidation.TestHelper;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class AsignarCargoSkillRequestValidatorTests
{
    private static readonly Guid NivelIdValido = Guid.Parse("70000000-0000-0000-0000-000000000001");

    private static AsignarCargoSkillRequest RequestValido(
        decimal? ponderacion = null,
        bool? esObligatoria = null,
        Guid? nivelRequeridoId = null)
        => new(
            NivelRequeridoId: nivelRequeridoId ?? NivelIdValido,
            Ponderacion: ponderacion,
            EsObligatoria: esObligatoria);

    private readonly AsignarCargoSkillRequestValidator _validator = new();

    // ── NivelRequeridoId ────────────────────────────────────────

    [Fact]
    public void Should_Have_Error_When_NivelRequeridoId_Is_Empty()
    {
        var request = RequestValido(nivelRequeridoId: Guid.Empty);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.NivelRequeridoId);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_NivelRequeridoId()
    {
        var request = RequestValido();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.NivelRequeridoId);
    }

    // ── Ponderacion — Reglas de rango ────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Should_Have_Error_When_Ponderacion_Is_Not_Positive(decimal ponderacion)
    {
        var request = RequestValido(ponderacion: ponderacion);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Ponderacion);
    }

    [Theory]
    [InlineData(100.01)]
    [InlineData(150)]
    public void Should_Have_Error_When_Ponderacion_Exceeds_100(decimal ponderacion)
    {
        var request = RequestValido(ponderacion: ponderacion);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Ponderacion);
    }

    // ── Ponderacion — Precisión ──────────────────────────────────

    [Theory]
    [InlineData(1.001)]
    [InlineData(1.257)]
    [InlineData(99.999)]
    public void Should_Have_Error_When_Ponderacion_Has_More_Than_Two_Decimals(decimal ponderacion)
    {
        var request = RequestValido(ponderacion: ponderacion);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Ponderacion);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Ponderacion_Is_Null()
    {
        var request = RequestValido(ponderacion: null);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Ponderacion);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(1.00)]
    [InlineData(2.50)]
    [InlineData(100.00)]
    public void Should_Not_Have_Error_When_Ponderacion_Is_In_Range(decimal ponderacion)
    {
        var request = RequestValido(ponderacion: ponderacion);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Ponderacion);
    }

    // ── EsObligatoria ───────────────────────────────────────────

    [Fact]
    public void Should_Not_Have_Error_When_EsObligatoria_Is_Null()
    {
        var request = RequestValido(esObligatoria: null);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_Not_Have_Error_When_EsObligatoria_Is_Bool(bool esObligatoria)
    {
        var request = RequestValido(esObligatoria: esObligatoria);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Request válido completo ─────────────────────────────────

    [Fact]
    public void Should_Not_Have_Any_Error_For_Valid_Request()
    {
        var request = new AsignarCargoSkillRequest(
            NivelRequeridoId: NivelIdValido,
            Ponderacion: 2.50m,
            EsObligatoria: true);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}