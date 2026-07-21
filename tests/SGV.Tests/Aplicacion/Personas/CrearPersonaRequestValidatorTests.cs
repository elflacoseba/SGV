using FluentValidation.TestHelper;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Comandos.Validaciones;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Personas;

public sealed class CrearPersonaRequestValidatorTests
{
    private static CrearPersonaRequest RequestValido() => new(
        Legajo: "LEG-001",
        Nombres: "Juan",
        Apellidos: "Pérez",
        Email: "juan@test.com",
        // Issue #147: TipoDocumentoId reemplaza al string TipoDocumento.
        // PR2: ahora referencia el Guid seed de DNI para validar FK + patrón
        // + longitud contra el catálogo in-memory de tests.
        TipoDocumentoId: TipoDocumentoConstantes.DniId,
        NumeroDocumento: "12345678",
        Telefono: "555-0101");

    private readonly CrearPersonaRequestValidator _validator =
        new(new FakeTipoDocumentoCatalogoConsulta());

    // ── Legajo ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Legajo_Is_Empty(string? legajo)
    {
        var request = RequestValido() with { Legajo = legajo! };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.Legajo);
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
        // Guid fuera del catálogo seed: el catalog fake sólo conoce DNI/LE/LC/Pasaporte.
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
        // DNI requiere 7-8 dígitos puros; letras ⇒ patrón inválido.
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
        // DNI rango 7-8 dígitos. 5 dígitos cae fuera del rango.
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
        // DNI rango 7-8 dígitos. 9 dígitos cae fuera del rango.
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
        // Pasaporte requiere ^[A-Za-z]{3}\d{6}$. Sin letras al inicio ⇒ inválido.
        var request = RequestValido() with
        {
            TipoDocumentoId = TipoDocumentoConstantes.PasaporteId,
            NumeroDocumento = "123456789"
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.NumeroDocumento)
            .WithErrorCode("PATRON_NO_CUMPLIDO");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Pasaporte_Valido()
    {
        // Pasaporte válido: AAA123456
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
        // Documento opcional: ambos null no deben disparar validaciones de catálogo.
        var request = RequestValido() with
        {
            TipoDocumentoId = null,
            NumeroDocumento = null
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_FK_INEXISTENTE_AntesQue_OtrasValidaciones()
    {
        // Si TipoDocumentoId no existe en el catálogo, NO se valida patrón/longitud
        // (porque no hay patrón contra qué validar). El error es FK_INEXISTENTE.
        var idFueraDeCatalogo = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var request = RequestValido() with
        {
            TipoDocumentoId = idFueraDeCatalogo,
            NumeroDocumento = "ABC" // También sería inválido para cualquier catálogo
        };

        var result = _validator.TestValidateAsync(request).GetAwaiter().GetResult();

        result.ShouldHaveValidationErrorFor(r => r.TipoDocumentoId)
            .WithErrorCode("FK_INEXISTENTE");
    }
}

internal sealed class FakeTipoDocumentoCatalogoConsulta : ITipoDocumentoCatalogoConsulta
{
    private static readonly IReadOnlyList<TipoDocumentoDto> Seed =
    [
        new(TipoDocumentoConstantes.DniId,
            TipoDocumentoConstantes.DniCodigo,
            TipoDocumentoConstantes.DniNombre,
            TipoDocumentoConstantes.DniPatron,
            TipoDocumentoConstantes.DniLongitudMinima,
            TipoDocumentoConstantes.DniLongitudMaxima),
        new(TipoDocumentoConstantes.LeId,
            TipoDocumentoConstantes.LeCodigo,
            TipoDocumentoConstantes.LeNombre,
            TipoDocumentoConstantes.LePatron,
            TipoDocumentoConstantes.LeLongitudMinima,
            TipoDocumentoConstantes.LeLongitudMaxima),
        new(TipoDocumentoConstantes.LcId,
            TipoDocumentoConstantes.LcCodigo,
            TipoDocumentoConstantes.LcNombre,
            TipoDocumentoConstantes.LcPatron,
            TipoDocumentoConstantes.LcLongitudMinima,
            TipoDocumentoConstantes.LcLongitudMaxima),
        new(TipoDocumentoConstantes.PasaporteId,
            TipoDocumentoConstantes.PasaporteCodigo,
            TipoDocumentoConstantes.PasaporteNombre,
            TipoDocumentoConstantes.PasaportePatron,
            TipoDocumentoConstantes.PasaporteLongitudMinima,
            TipoDocumentoConstantes.PasaporteLongitudMaxima)
    ];

    public Task<IReadOnlyList<TipoDocumentoDto>> ListarAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Seed);

    public Task<TipoDocumentoDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Seed.FirstOrDefault(t => t.Id == id));
}
