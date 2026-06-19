using FluentValidation.TestHelper;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Comandos.Validaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

public sealed class CrearHabilidadRequestValidatorTests
{
    private static CrearHabilidadRequest RequestValido() => new(
        Codigo: "COM01",
        Nombre: "Comunicación");

    private readonly CrearHabilidadRequestValidator _validator = new();

    // ── Codigo ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Codigo_Is_Empty(string? codigo)
    {
        var request = RequestValido() with { Codigo = codigo! };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Codigo);
    }

    [Fact]
    public void Should_Have_Error_When_Codigo_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Codigo = new string('X', 51) };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Codigo);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Codigo()
    {
        var request = RequestValido() with { Codigo = "COM01" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Codigo);
    }

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
        var request = RequestValido() with { Nombre = "Comunicación" };

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

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Categoria()
    {
        var request = RequestValido() with { Categoria = "Blandas" };

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

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Descripcion()
    {
        var request = RequestValido() with { Descripcion = "Una descripción válida." };

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
