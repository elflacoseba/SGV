using FluentValidation.TestHelper;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class ActualizarPuestoRequestValidatorTests
{
    private static ActualizarPuestoRequest RequestValido() => new(
        Nombre: "Gerente General Actualizado");

    private readonly ActualizarPuestoRequestValidator _validator = new();

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
        var request = RequestValido() with { Nombre = "Gerente General" };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Nombre);
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
            Descripcion = "Descripción opcional",
            PuestoSuperiorId = Guid.NewGuid()
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
