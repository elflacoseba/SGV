using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Dominio.Habilidades;

/// <summary>
/// Tests del dominio para <see cref="CategoriaHabilidad"/> (catálogo inmutable
/// seed-only, bloque GUID <c>72000000-…</c>). Valida invariantes de shape del
/// factory <c>Reconstitute</c>: <c>Codigo</c> y <c>Nombre</c> requeridos con
/// longitudes máximas según <see cref="CategoriaHabilidadRules"/>.
/// </summary>
public sealed class CategoriaHabilidadDominioTests
{
    private static readonly Guid ConduccionId = Guid.Parse("72000000-0000-0000-0000-000000000000");

    // ── Reconstitute: invariantes de shape ──────────────────────

    [Fact]
    public void Reconstitute_IdCodigoYNombre_AsignaPropiedades()
    {
        var id = Guid.NewGuid();
        var categoria = CategoriaHabilidad.Reconstitute(id, "Conduccion", "Conducción");

        Assert.Equal(id, categoria.Id);
        Assert.Equal("Conduccion", categoria.Codigo);
        Assert.Equal("Conducción", categoria.Nombre);
    }

    [Fact]
    public void Reconstitute_CodigoVacio_LanzaArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            CategoriaHabilidad.Reconstitute(Guid.NewGuid(), "", "Conducción"));

        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Reconstitute_CodigoWhitespace_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CategoriaHabilidad.Reconstitute(Guid.NewGuid(), "   ", "Conducción"));
    }

    [Fact]
    public void Reconstitute_NombreVacio_LanzaArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            CategoriaHabilidad.Reconstitute(Guid.NewGuid(), "Conduccion", ""));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Reconstitute_NombreWhitespace_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CategoriaHabilidad.Reconstitute(Guid.NewGuid(), "Conduccion", "   "));
    }

    [Fact]
    public void Reconstitute_CodigoSuperaMaxLength_LanzaArgumentException()
    {
        var codigoLargo = new string('X', CategoriaHabilidadRules.CodigoMaxLength + 1);
        var ex = Assert.Throws<ArgumentException>(() =>
            CategoriaHabilidad.Reconstitute(Guid.NewGuid(), codigoLargo, "Conducción"));

        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Reconstitute_NombreSuperaMaxLength_LanzaArgumentException()
    {
        var nombreLargo = new string('X', CategoriaHabilidadRules.NombreMaxLength + 1);
        var ex = Assert.Throws<ArgumentException>(() =>
            CategoriaHabilidad.Reconstitute(Guid.NewGuid(), "Conduccion", nombreLargo));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Reconstitute_CodigoEnMaxLength_TrimYAsigna()
    {
        // El límite es 50; una cadena de exactamente 50 chars NO debe lanzar.
        var codigo = new string('X', CategoriaHabilidadRules.CodigoMaxLength);
        var categoria = CategoriaHabilidad.Reconstitute(Guid.NewGuid(), codigo, "Conducción");

        Assert.Equal(codigo, categoria.Codigo);
    }

    [Fact]
    public void Reconstitute_CodigoConEspaciosAlrededor_TrimNormaliza()
    {
        var categoria = CategoriaHabilidad.Reconstitute(ConduccionId, "  Conduccion  ", "Conducción");

        Assert.Equal("Conduccion", categoria.Codigo);
    }

    // ── Constants de CategoriaHabilidadRules ───────────────────

    [Fact]
    public void CategoriaHabilidadRules_CodigoMaxLengthEs50()
    {
        Assert.Equal(50, CategoriaHabilidadRules.CodigoMaxLength);
    }

    [Fact]
    public void CategoriaHabilidadRules_NombreMaxLengthEs100()
    {
        Assert.Equal(100, CategoriaHabilidadRules.NombreMaxLength);
    }
}