using FluentValidation.TestHelper;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class CrearUnidadOrganizativaRequestValidatorTests
{
    private static readonly Guid TipoIdValido = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static CrearUnidadOrganizativaRequest RequestValido() => new(
        Codigo: "GER",
        Nombre: "Gerencia General",
        TipoUnidadOrganizativaId: TipoIdValido);

    private readonly CrearUnidadOrganizativaRequestValidator _validator = new();

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
        var request = RequestValido() with { Codigo = "GER" };

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
        var request = RequestValido() with { Nombre = "Gerencia General" };

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

    // ── TipoUnidadOrganizativaId ───────────────────────────────

    [Fact]
    public void Should_Have_Error_When_TipoUnidadOrganizativaId_Is_Empty()
    {
        var request = RequestValido() with { TipoUnidadOrganizativaId = Guid.Empty };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.TipoUnidadOrganizativaId);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_TipoUnidadOrganizativaId()
    {
        var request = RequestValido();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.TipoUnidadOrganizativaId);
    }

    // ── Vigencia ───────────────────────────────────────────────

    [Fact]
    public void Should_Have_Error_When_VigenteHasta_Is_Before_VigenteDesde()
    {
        var request = RequestValido() with
        {
            VigenteDesde = new DateOnly(2025, 6, 1),
            VigenteHasta = new DateOnly(2025, 5, 1)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.VigenteHasta);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Vigencia()
    {
        var request = RequestValido() with
        {
            VigenteDesde = new DateOnly(2025, 1, 1),
            VigenteHasta = new DateOnly(2025, 12, 31)
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.VigenteHasta);
    }

    [Fact]
    public void Should_Not_Have_Error_When_VigenteHasta_Equals_VigenteDesde()
    {
        var request = RequestValido() with
        {
            VigenteDesde = new DateOnly(2025, 6, 15),
            VigenteHasta = new DateOnly(2025, 6, 15)
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.VigenteHasta);
    }

    // ── Request válido completo ───────────────────────────────

    [Fact]
    public void Should_Not_Have_Any_Error_For_Valid_Request()
    {
        var request = RequestValido() with
        {
            Descripcion = "Descripción opcional",
            VigenteDesde = new DateOnly(2025, 1, 1),
            VigenteHasta = new DateOnly(2025, 12, 31),
            UnidadPadreId = Guid.NewGuid()
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
