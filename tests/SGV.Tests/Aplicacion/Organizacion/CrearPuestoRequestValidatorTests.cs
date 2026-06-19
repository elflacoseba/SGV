using FluentValidation.TestHelper;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class CrearPuestoRequestValidatorTests
{
    private static readonly Guid UnidadIdValida = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid CargoIdValido = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    private static CrearPuestoRequest RequestValido() => new(
        Codigo: "GER-001",
        Nombre: "Gerente General",
        UnidadOrganizativaId: UnidadIdValida,
        CargoId: CargoIdValido);

    private readonly CrearPuestoRequestValidator _validator = new();

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
        var request = RequestValido() with { Codigo = "GER-001" };

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

    // ── UnidadOrganizativaId ──────────────────────────────────

    [Fact]
    public void Should_Have_Error_When_UnidadOrganizativaId_Is_Empty()
    {
        var request = RequestValido() with { UnidadOrganizativaId = Guid.Empty };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.UnidadOrganizativaId);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_UnidadOrganizativaId()
    {
        var request = RequestValido();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.UnidadOrganizativaId);
    }

    // ── CargoId ────────────────────────────────────────────────

    [Fact]
    public void Should_Have_Error_When_CargoId_Is_Empty()
    {
        var request = RequestValido() with { CargoId = Guid.Empty };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.CargoId);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_CargoId()
    {
        var request = RequestValido();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.CargoId);
    }

    // ── Request válido completo ───────────────────────────────

    [Fact]
    public void Should_Not_Have_Any_Error_For_Valid_Request()
    {
        var request = RequestValido() with
        {
            Descripcion = "Descripción opcional"
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
