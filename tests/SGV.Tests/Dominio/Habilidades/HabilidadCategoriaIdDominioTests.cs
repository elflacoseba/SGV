using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Dominio.Habilidades;

/// <summary>
/// Tests del dominio para <see cref="Habilidad"/> que validan la nueva
/// firma con <c>CategoriaId</c> (Guid?) en lugar de la legacy
/// <c>Categoria</c> (string?). La navigation <c>Categoria</c> se mantiene
/// como propiedad separada para cargar el catálogo hidratado cuando el
/// repositorio hace JOIN.
/// </summary>
public sealed class HabilidadCategoriaIdDominioTests
{
    private static readonly Guid ConduccionId = Guid.Parse("72000000-0000-0000-0000-000000000000");

    // ── Constructor con CategoriaId ─────────────────────────────

    [Fact]
    public void Constructor_SinCategoriaId_AsignaNull()
    {
        var habilidad = new Habilidad("PROG", "Programación", categoriaId: null, descripcion: null);

        Assert.Null(habilidad.CategoriaId);
        Assert.Null(habilidad.Categoria);
    }

    [Fact]
    public void Constructor_ConCategoriaId_AsignaGuid()
    {
        var habilidad = new Habilidad("PROG", "Programación", categoriaId: ConduccionId, descripcion: null);

        Assert.Equal(ConduccionId, habilidad.CategoriaId);
        Assert.Null(habilidad.Categoria);
    }

    [Fact]
    public void Constructor_ConCategoriaIdVacio_AsignaEmpty()
    {
        // Guid.Empty es válido como valor (la validación contra catálogo es en servicio).
        var habilidad = new Habilidad("PROG", "Programación", categoriaId: Guid.Empty, descripcion: null);

        Assert.Equal(Guid.Empty, habilidad.CategoriaId);
    }

    // ── Categoria legacy NO existe más ─────────────────────────

    [Fact]
    public void Habilidad_NoExponePropiedadCategoriaString()
    {
        // Garantía estructural: el campo legacy 'Categoria' (string) desapareció.
        // Si alguien re-introduce la propiedad legacy, este test falla.
        var tipo = typeof(Habilidad);
        var tieneCategoriaString = tipo.GetProperty("Categoria")?.PropertyType == typeof(string);

        Assert.False(tieneCategoriaString,
            "Habilidad NO debe exponer una propiedad 'Categoria' de tipo string.");
    }

    // ── Actualizar reemplaza CategoriaId ────────────────────────

    [Fact]
    public void Actualizar_ConNuevoCategoriaId_ReemplazaGuid()
    {
        var habilidad = new Habilidad("PROG", "Programación", categoriaId: ConduccionId, descripcion: null);

        var nuevaId = Guid.Parse("72000000-0000-0000-0000-000000000001");
        habilidad.Actualizar("PROG", "Programación", categoriaId: nuevaId, descripcion: null);

        Assert.Equal(nuevaId, habilidad.CategoriaId);
    }

    [Fact]
    public void Actualizar_ConCategoriaIdNull_LimpiaCategoriaId()
    {
        var habilidad = new Habilidad("PROG", "Programación", categoriaId: ConduccionId, descripcion: null);

        habilidad.Actualizar("PROG", "Programación", categoriaId: null, descripcion: null);

        Assert.Null(habilidad.CategoriaId);
    }

    // ── Reconstitute con CategoriaId ────────────────────────────

    [Fact]
    public void Reconstitute_ConCategoriaIdNull_PreservaNull()
    {
        var id = Guid.NewGuid();
        var fecha = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var habilidad = Habilidad.Reconstitute(
            id,
            codigo: "PROG",
            nombre: "Programación",
            categoriaId: null,
            descripcion: null,
            isActive: true,
            fecha,
            createdByUserId: null,
            updatedAt: null,
            updatedByUserId: null,
            isDeleted: false,
            deletedAt: null,
            deletedByUserId: null);

        Assert.Null(habilidad.CategoriaId);
        Assert.Null(habilidad.Categoria);
    }

    [Fact]
    public void Reconstitute_ConCategoriaId_PreservaGuid()
    {
        var id = Guid.NewGuid();
        var fecha = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var habilidad = Habilidad.Reconstitute(
            id,
            codigo: "PROG",
            nombre: "Programación",
            categoriaId: ConduccionId,
            descripcion: null,
            isActive: true,
            fecha,
            createdByUserId: null,
            updatedAt: null,
            updatedByUserId: null,
            isDeleted: false,
            deletedAt: null,
            deletedByUserId: null);

        Assert.Equal(ConduccionId, habilidad.CategoriaId);
    }
}