using FluentValidation.TestHelper;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Comandos.Validaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

/// <summary>
/// Tests del validador FluentValidation de <see cref="CrearHabilidadRequest"/>.
///
/// <b>Issue migrar-campo-categoria-habilidades-a-tabla:</b> el campo legacy
/// <c>string? Categoria</c> se reemplazó por <c>Guid? CategoriaId</c>; la
/// validación contra catálogo la hace el servicio (no el validador).
/// </summary>
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

    // ── CategoriaId ───────────────────────────────────────────

    [Fact]
    public void Should_Not_Have_Error_For_Null_CategoriaId()
    {
        var request = RequestValido() with { CategoriaId = null };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.CategoriaId);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_CategoriaId()
    {
        var request = RequestValido()
            with { CategoriaId = Guid.Parse("72000000-0000-0000-0000-000000000000") };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.CategoriaId);
    }

    [Fact]
    public void Should_Have_Error_When_CategoriaId_Is_Empty()
    {
        var request = RequestValido() with { CategoriaId = Guid.Empty };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.CategoriaId!.Value);
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
            CategoriaId = Guid.Parse("72000000-0000-0000-0000-000000000000"),
            Descripcion = "Descripción opcional"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}