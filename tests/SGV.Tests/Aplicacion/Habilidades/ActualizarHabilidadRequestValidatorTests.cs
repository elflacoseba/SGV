using FluentValidation.TestHelper;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Comandos.Validaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

public sealed class ActualizarHabilidadRequestValidatorTests
{
    private static ActualizarHabilidadRequest RequestValido() => new(
        Nombre: "Comunicación Efectiva");

    private readonly ActualizarHabilidadRequestValidator _validator = new();

    // ── Nombre ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Nombre_Is_Empty(string? nombre)
    {
        var request = RequestValido() with { Nombre = nombre! };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Nombre);
    }

    [Fact]
    public void Should_Have_Error_When_Nombre_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Nombre = new string('X', 201) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Nombre);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Nombre()
    {
        var request = RequestValido() with { Nombre = "Comunicación Efectiva" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Nombre);
    }

    // ── Categoria ─────────────────────────────────────────────

    [Fact]
    public void Should_Have_Error_When_Categoria_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Categoria = new string('X', 101) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Categoria!);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Null_Categoria()
    {
        var request = RequestValido() with { Categoria = null };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Categoria);
    }

    // ── Descripcion ────────────────────────────────────────────

    [Fact]
    public void Should_Have_Error_When_Descripcion_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Descripcion = new string('X', 1001) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Descripcion!);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Null_Descripcion()
    {
        var request = RequestValido() with { Descripcion = null };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Descripcion);
    }

    // ── Request válido completo ───────────────────────────────

    [Fact]
    public void Should_Not_Have_Any_Error_For_Valid_Request()
    {
        var request = RequestValido() with
        {
            Categoria = "Blandas",
            Descripcion = "Descripción opcional"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
