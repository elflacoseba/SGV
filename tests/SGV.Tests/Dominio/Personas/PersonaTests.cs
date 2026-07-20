using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Dominio.Personas;

public sealed class PersonaTests
{
    // ── Constructor ─────────────────────────────────────────────

    [Fact]
    public void Crear_ConValoresValidos_AsignaPropiedades()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001", "juan@test.com");

        Assert.NotEqual(Guid.Empty, persona.Id);
        Assert.Equal("LEG-001", persona.Legajo);
        Assert.Equal("Juan", persona.Nombres);
        Assert.Equal("Pérez", persona.Apellidos);
        Assert.Equal("juan@test.com", persona.Email);
        Assert.True(persona.IsActive);
    }

    [Fact]
    public void Crear_SinLegajoYEmail_AsignaNulos()
    {
        var persona = new Persona("Juan", "Pérez");

        Assert.Null(persona.Legajo);
        Assert.Null(persona.Email);
        Assert.True(persona.IsActive);
    }

    [Fact]
    public void Crear_ConNombresVacios_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Persona("", "Pérez", "LEG-001"));

        Assert.Contains("Nombres", ex.Message);
    }

    [Fact]
    public void Crear_ConApellidosVacios_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Persona("Juan", "", "LEG-001"));

        Assert.Contains("Apellidos", ex.Message);
    }

    // ── CambiarDatos ────────────────────────────────────────────

    [Fact]
    public void CambiarDatos_ModificaCamposEditables()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001", "juan@test.com");

        persona.CambiarDatos("Ana", "García", "LEG-002", "ana@test.com", "555-0101");

        Assert.Equal("Ana", persona.Nombres);
        Assert.Equal("García", persona.Apellidos);
        Assert.Equal("LEG-002", persona.Legajo);
        Assert.Equal("ana@test.com", persona.Email);
        Assert.Equal("555-0101", persona.Telefono);
    }

    [Fact]
    public void CambiarDatos_PermiteLegajoEmailYTelefonoNulos()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001", "juan@test.com");

        persona.CambiarDatos("Juan", "Pérez", null, null, null);

        Assert.Null(persona.Legajo);
        Assert.Null(persona.Email);
        Assert.Null(persona.Telefono);
    }

    [Fact]
    public void CambiarDatos_ConNombresVacio_ThrowsArgumentException()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001");

        var ex = Assert.Throws<ArgumentException>(
            () => persona.CambiarDatos("", "García", null));

        Assert.Contains("Nombres", ex.Message);
    }

    [Fact]
    public void CambiarDatos_ConApellidosVacio_ThrowsArgumentException()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001");

        var ex = Assert.Throws<ArgumentException>(
            () => persona.CambiarDatos("Juan", "", null));

        Assert.Contains("Apellidos", ex.Message);
    }

    [Fact]
    public void CambiarDatos_ConLegajoMayorA50_ThrowsArgumentException()
    {
        var persona = new Persona("Juan", "Pérez");
        var legajoLargo = new string('A', 51);

        var ex = Assert.Throws<ArgumentException>(
            () => persona.CambiarDatos("Juan", "Pérez", legajoLargo));

        Assert.Contains("Legajo", ex.Message);
    }

    [Fact]
    public void CambiarDatos_ConEmailMayorA320_ThrowsArgumentException()
    {
        var persona = new Persona("Juan", "Pérez");
        var emailLargo = new string('A', 321) + "@test.com";

        var ex = Assert.Throws<ArgumentException>(
            () => persona.CambiarDatos("Juan", "Pérez", null, emailLargo));

        Assert.Contains("Email", ex.Message);
    }

    // ── CambiarDocumento ────────────────────────────────────────

    [Fact]
    public void CambiarDocumento_AsignaTipoYNumero()
    {
        var persona = new Persona("Juan", "Pérez");
        var dniId = Guid.NewGuid();

        persona.CambiarDocumento(dniId, "12345678");

        Assert.Equal(dniId, persona.TipoDocumentoId);
        Assert.Equal("12345678", persona.NumeroDocumento);
    }

    [Fact]
    public void CambiarDocumento_PermiteValoresNulos()
    {
        var persona = new Persona("Juan", "Pérez");
        persona.CambiarDocumento(Guid.NewGuid(), "12345678");

        persona.CambiarDocumento(null, null);

        Assert.Null(persona.TipoDocumentoId);
        Assert.Null(persona.NumeroDocumento);
    }

    [Fact]
    public void CambiarDocumento_ConTipoGuidVacio_PermiteAsignarExplicitamente()
    {
        // Guid.Empty en TipoDocumentoId NO se normaliza a null en el Dominio:
        // el caller decide el contrato. La validación "Guid.Empty no
        // permitido cuando se informa documento" se enforce en PR2 (T14).
        var persona = new Persona("Juan", "Pérez");

        persona.CambiarDocumento(Guid.Empty, "12345678");

        Assert.Equal(Guid.Empty, persona.TipoDocumentoId);
        Assert.Equal("12345678", persona.NumeroDocumento);
    }

    [Fact]
    public void CambiarDocumento_ConNumeroMayorA50_ThrowsArgumentException()
    {
        var persona = new Persona("Juan", "Pérez");
        var numeroLargo = new string('A', 51);

        var ex = Assert.Throws<ArgumentException>(
            () => persona.CambiarDocumento(Guid.NewGuid(), numeroLargo));

        Assert.Contains("NumeroDocumento", ex.Message);
    }

    [Fact]
    public void CambiarDocumento_TransicionDeUnTipoAOtro()
    {
        var persona = new Persona("Juan", "Pérez");
        var dniId = Guid.NewGuid();
        var pasaporteId = Guid.NewGuid();

        persona.CambiarDocumento(dniId, "12345678");
        Assert.Equal(dniId, persona.TipoDocumentoId);

        persona.CambiarDocumento(pasaporteId, "ABC123456");
        Assert.Equal(pasaporteId, persona.TipoDocumentoId);
    }

    [Fact]
    public void CambiarDocumento_TransicionDeNullAGuid_AsignaGuid()
    {
        var persona = new Persona("Juan", "Pérez");
        persona.CambiarDocumento(null, null);
        Assert.Null(persona.TipoDocumentoId);

        var dniId = Guid.NewGuid();
        persona.CambiarDocumento(dniId, "12345678");
        Assert.Equal(dniId, persona.TipoDocumentoId);
    }

    [Fact]
    public void CambiarDocumento_TransicionDeGuidANull()
    {
        var persona = new Persona("Juan", "Pérez");
        persona.CambiarDocumento(Guid.NewGuid(), "12345678");

        persona.CambiarDocumento(null, null);
        Assert.Null(persona.TipoDocumentoId);
        Assert.Null(persona.NumeroDocumento);
    }

    // ── Desactivar ──────────────────────────────────────────────

    [Fact]
    public void Desactivar_SeteaIsActiveFalse()
    {
        var persona = new Persona("Juan", "Pérez");

        persona.Desactivar();

        Assert.False(persona.IsActive);
    }

    [Fact]
    public void Desactivar_NoCambiaNombres()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001");

        persona.Desactivar();

        Assert.Equal("Juan", persona.Nombres);
    }

    [Fact]
    public void Desactivar_NoCambiaApellidos()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001");

        persona.Desactivar();

        Assert.Equal("Pérez", persona.Apellidos);
    }

    [Fact]
    public void Desactivar_NoCambiaLegajo()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001");

        persona.Desactivar();

        Assert.Equal("LEG-001", persona.Legajo);
    }

    [Fact]
    public void Desactivar_PersonaYaInactiva_MantieneIsActiveFalse()
    {
        var persona = new Persona("Juan", "Pérez");
        persona.Desactivar();

        persona.Desactivar();

        Assert.False(persona.IsActive);
    }

    // ── Activar ─────────────────────────────────────────────────

    [Fact]
    public void Activar_PersonaInactiva_SeteaIsActiveTrue()
    {
        var persona = new Persona("Juan", "Pérez");
        persona.Desactivar();

        persona.Activar();

        Assert.True(persona.IsActive);
    }

    [Fact]
    public void Activar_NoCambiaNombres()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001");
        persona.Desactivar();

        persona.Activar();

        Assert.Equal("Juan", persona.Nombres);
    }

    [Fact]
    public void Activar_NoCambiaApellidos()
    {
        var persona = new Persona("Juan", "Pérez", "LEG-001");
        persona.Desactivar();

        persona.Activar();

        Assert.Equal("Pérez", persona.Apellidos);
    }

    [Fact]
    public void Activar_PersonaYaActiva_MantieneIsActiveTrue()
    {
        var persona = new Persona("Juan", "Pérez");

        persona.Activar();

        Assert.True(persona.IsActive);
    }

    // ── Colecciones no expuestas ────────────────────────────────

    [Fact]
    public void Habilidades_RetornaColeccionVacia()
    {
        var persona = new Persona("Juan", "Pérez");

        Assert.Empty(persona.Habilidades);
    }

    [Fact]
    public void Ocupaciones_RetornaColeccionVacia()
    {
        var persona = new Persona("Juan", "Pérez");

        Assert.Empty(persona.Ocupaciones);
    }
}
