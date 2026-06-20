using FluentValidation.TestHelper;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Comandos.Validaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Personas;

public sealed class CrearPersonaRequestValidatorTests
{
    private static CrearPersonaRequest RequestValido() => new(
        Legajo: "LEG-001",
        Nombres: "Juan",
        Apellidos: "Pérez",
        Email: "juan@test.com",
        TipoDocumento: "DNI",
        NumeroDocumento: "12345678",
        Telefono: "555-0101");

    private readonly CrearPersonaRequestValidator _validator = new();

    // ── Legajo ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Legajo_Is_Empty(string? legajo)
    {
        var request = RequestValido() with { Legajo = legajo! };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Legajo);
    }

    [Fact]
    public void Should_Have_Error_When_Legajo_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Legajo = new string('X', 51) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Legajo);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Legajo()
    {
        var request = RequestValido() with { Legajo = "LEG-001" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Legajo);
    }

    // ── Nombres ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Nombres_Is_Empty(string? nombres)
    {
        var request = RequestValido() with { Nombres = nombres! };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Nombres);
    }

    [Fact]
    public void Should_Have_Error_When_Nombres_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Nombres = new string('X', 101) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Nombres);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Nombres()
    {
        var request = RequestValido() with { Nombres = "Juan" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Nombres);
    }

    // ── Apellidos ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Apellidos_Is_Empty(string? apellidos)
    {
        var request = RequestValido() with { Apellidos = apellidos! };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Apellidos);
    }

    [Fact]
    public void Should_Have_Error_When_Apellidos_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Apellidos = new string('X', 101) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Apellidos);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Apellidos()
    {
        var request = RequestValido() with { Apellidos = "Pérez" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Apellidos);
    }

    // ── Email (opcional, formato cuando se informa) ────────────

    [Fact]
    public void Should_Not_Have_Error_When_Email_Is_Null()
    {
        var request = RequestValido() with { Email = null };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Email_Is_Empty()
    {
        var request = RequestValido() with { Email = "" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Email = new string('A', 321) + "@test.com" };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Has_Invalid_Format()
    {
        var request = RequestValido() with { Email = "no-es-un-email" };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Email()
    {
        var request = RequestValido() with { Email = "juan@test.com" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Email);
    }

    // ── Documento (opcional) ──────────────────────────────────

    [Fact]
    public void Should_Not_Have_Error_When_TipoDocumento_Is_Null()
    {
        var request = RequestValido() with { TipoDocumento = null };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.TipoDocumento);
    }

    [Fact]
    public void Should_Not_Have_Error_When_NumeroDocumento_Is_Null()
    {
        var request = RequestValido() with { NumeroDocumento = null };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.NumeroDocumento);
    }

    [Fact]
    public void Should_Have_Error_When_TipoDocumento_Exceeds_Max_Length()
    {
        var request = RequestValido() with { TipoDocumento = new string('X', 51) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.TipoDocumento);
    }

    [Fact]
    public void Should_Have_Error_When_NumeroDocumento_Exceeds_Max_Length()
    {
        var request = RequestValido() with { NumeroDocumento = new string('X', 51) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.NumeroDocumento);
    }

    // ── Telefono (opcional) ───────────────────────────────────

    [Fact]
    public void Should_Not_Have_Error_When_Telefono_Is_Null()
    {
        var request = RequestValido() with { Telefono = null };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Telefono);
    }

    [Fact]
    public void Should_Have_Error_When_Telefono_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Telefono = new string('X', 51) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Telefono);
    }

    // ── Request válido completo ───────────────────────────────

    [Fact]
    public void Should_Not_Have_Any_Error_For_Valid_Request()
    {
        var request = RequestValido();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
