using FluentValidation.TestHelper;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Comandos.Validaciones;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Personas;

public sealed class ActualizarPersonaRequestValidatorTests
{
    private static ActualizarPersonaRequest RequestValido() => new(
        Legajo: "LEG-001",
        Nombres: "Juan",
        Apellidos: "Pérez",
        Email: "juan@test.com",
        // Issue #147: PR2 referencia el Guid seed de DNI para validar FK +
        // patrón + longitud contra el catálogo in-memory de tests.
        TipoDocumentoId: TipoDocumentoConstantes.DniId,
        NumeroDocumento: "12345678",
        Telefono: "555-0101");

    private readonly ActualizarPersonaRequestValidator _validator =
        new(new FakeTipoDocumentoCatalogoConsulta());

    // ── Legajo ────────────────────────────────────────────────
    //
    // Política vigente: Legajo es opcional. El dominio (Persona) lo
    // permite null/vacío (ValidacionesDominio.Opcional), la columna
    // `Personas.Legajo` es nullable, y el bootstrap del primer
    // Administrador (issue #195) lo acepta. Por eso NO exigimos
    // NotEmpty; sólo se valida el largo máximo cuando hay valor.

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Not_Have_Error_When_Legajo_Is_Empty(string? legajo)
    {
        var request = RequestValido() with { Legajo = legajo! };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.Legajo);
    }

    [Fact]
    public void Should_Have_Error_When_Legajo_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Legajo = new string('X', 51) };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Legajo);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Legajo()
    {
        var request = RequestValido() with { Legajo = "LEG-001" };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

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

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Nombres);
    }

    [Fact]
    public void Should_Have_Error_When_Nombres_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Nombres = new string('X', 101) };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Nombres);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Nombres()
    {
        var request = RequestValido() with { Nombres = "Juan" };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

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

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Apellidos);
    }

    [Fact]
    public void Should_Have_Error_When_Apellidos_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Apellidos = new string('X', 101) };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Apellidos);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Apellidos()
    {
        var request = RequestValido() with { Apellidos = "Pérez" };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.Apellidos);
    }

    // ── Email (opcional, formato cuando se informa) ────────────

    [Fact]
    public void Should_Not_Have_Error_When_Email_Is_Null()
    {
        var request = RequestValido() with { Email = null };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Email_Is_Empty()
    {
        var request = RequestValido() with { Email = "" };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Email = new string('A', 321) + "@test.com" };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Has_Invalid_Format()
    {
        var request = RequestValido() with { Email = "no-es-un-email" };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Should_Not_Have_Error_For_Valid_Email()
    {
        var request = RequestValido() with { Email = "juan@test.com" };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.Email);
    }

    // ── Documento (opcional) ──────────────────────────────────

    [Fact]
    public void Should_Not_Have_Error_When_TipoDocumentoId_Is_Null()
    {
        var request = RequestValido() with { TipoDocumentoId = null };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.TipoDocumentoId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_NumeroDocumento_Is_Null()
    {
        var request = RequestValido() with { NumeroDocumento = null };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.NumeroDocumento);
    }

    [Fact]
    public void Should_Have_Error_When_TipoDocumentoId_Is_Empty()
    {
        var request = RequestValido() with { TipoDocumentoId = Guid.Empty };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.TipoDocumentoId);
    }

    [Fact]
    public void Should_Have_Error_When_NumeroDocumento_Exceeds_Max_Length()
    {
        var request = RequestValido() with { NumeroDocumento = new string('X', 51) };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.NumeroDocumento);
    }

    // ── Telefono (opcional) ───────────────────────────────────

    [Fact]
    public void Should_Not_Have_Error_When_Telefono_Is_Null()
    {
        var request = RequestValido() with { Telefono = null };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.Telefono);
    }

    [Fact]
    public void Should_Have_Error_When_Telefono_Exceeds_Max_Length()
    {
        var request = RequestValido() with { Telefono = new string('X', 51) };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Telefono);
    }

    // ── Request válido completo ───────────────────────────────

    [Fact]
    public void Should_Not_Have_Any_Error_For_Valid_Request()
    {
        var request = RequestValido();

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── PR2: validación contra ITipoDocumentoCatalogoConsulta ──

    [Fact]
    public void Should_Have_FK_INEXISTENTE_When_TipoDocumentoId_NoEstaEnCatalogo()
    {
        var idFueraDeCatalogo = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var request = RequestValido() with
        {
            TipoDocumentoId = idFueraDeCatalogo,
            NumeroDocumento = "12345678"
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.TipoDocumentoId)
            .WithErrorCode("FK_INEXISTENTE");
    }

    [Fact]
    public void Should_Have_PATRON_NO_CUMPLIDO_When_NumeroDocumento_NoMatcheaDni()
    {
        var request = RequestValido() with
        {
            TipoDocumentoId = TipoDocumentoConstantes.DniId,
            NumeroDocumento = "12A45678"
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.NumeroDocumento)
            .WithErrorCode("PATRON_NO_CUMPLIDO");
    }

    [Fact]
    public void Should_Have_LONGITUD_FUERA_DE_RANGO_When_NumeroDocumento_Tiene5Digitos_Dni()
    {
        var request = RequestValido() with
        {
            TipoDocumentoId = TipoDocumentoConstantes.DniId,
            NumeroDocumento = "12345"
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.NumeroDocumento)
            .WithErrorCode("LONGITUD_FUERA_DE_RANGO");
    }

    [Fact]
    public void Should_Have_LONGITUD_FUERA_DE_RANGO_When_NumeroDocumento_Tiene9Digitos_Dni()
    {
        var request = RequestValido() with
        {
            TipoDocumentoId = TipoDocumentoConstantes.DniId,
            NumeroDocumento = "123456789"
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.NumeroDocumento)
            .WithErrorCode("LONGITUD_FUERA_DE_RANGO");
    }

    [Fact]
    public void Should_Have_PATRON_NO_CUMPLIDO_When_Pasaporte_NoCumplePatron()
    {
        var request = RequestValido() with
        {
            TipoDocumentoId = TipoDocumentoConstantes.PasaporteId,
            NumeroDocumento = "12345"
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.NumeroDocumento)
            .WithErrorCode("PATRON_NO_CUMPLIDO");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Pasaporte_Valido()
    {
        var request = RequestValido() with
        {
            TipoDocumentoId = TipoDocumentoConstantes.PasaporteId,
            NumeroDocumento = "AAA123456"
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveValidationErrorFor(r => r.NumeroDocumento);
    }

    [Fact]
    public void Should_Not_Have_Error_When_TipoDocumentoIdYNumeroDocumento_SonNull()
    {
        var request = RequestValido() with
        {
            TipoDocumentoId = null,
            NumeroDocumento = null
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveAnyValidationErrors();
    }
}
