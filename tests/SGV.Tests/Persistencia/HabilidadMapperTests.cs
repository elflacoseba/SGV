using System.Reflection;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Reflection guard + behavior coverage for the Habilidad persistence mapper.
/// See issue #124: <c>ToDomain(HabilidadEntity)</c> must not call the internal
/// <c>SetProperty</c> reflection helper; instead it should delegate to
/// <c>Habilidad.Reconstitute(...)</c>.
/// </summary>
public sealed class HabilidadMapperTests
{
    private static readonly Guid Id = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    private static HabilidadEntity CrearEntidad(bool isActive, bool isDeleted = false)
    {
        return new HabilidadEntity
        {
            Id = Id,
            Codigo = "HAB-001",
            Nombre = "Liderazgo",
            Categoria = "Soft",
            Descripcion = "Capacidad de liderazgo",
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = "system"
        };
    }

    // ── IL reflection guard ─────────────────────────────────────

    [Fact]
    public void ToDomain_Habilidad_NoLlamaSetPropertyReflectionHelper()
    {
        var assembly = typeof(HabilidadRepository).Assembly;
        var mapperType = assembly.GetType(
            "SGV.Infraestructura.Persistencia.Mapeos.PersistenceToDomainMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            "ToDomain",
            new[] { typeof(HabilidadEntity) })
            ?? throw new InvalidOperationException(
                "PersistenceToDomainMapper.ToDomain(HabilidadEntity) not found.");
        var methodBody = method.GetMethodBody()
            ?? throw new InvalidOperationException(
                "ToDomain has no IL body (abstract/extern?).");
        var il = methodBody.GetILAsByteArray()
            ?? throw new InvalidOperationException(
                "ToDomain IL body returned no bytes.");
        var module = method.Module;

        MethodInfo? setPropertyCall = null;
        for (var i = 0; i < il.Length; i++)
        {
            if ((il[i] != 0x28 && il[i] != 0x6F) || i + 4 >= il.Length)
            {
                continue;
            }

            var token = BitConverter.ToInt32(il, i + 1);
            try
            {
                if (module.ResolveMethod(token) is MethodInfo called
                    && called.Name == "SetProperty"
                    && called.DeclaringType == mapperType)
                {
                    setPropertyCall = called;
                    break;
                }
            }
            catch (ArgumentException)
            {
                // Token may resolve to a field reference (ld*fld) rather than a method.
            }

            i += 4;
        }

        Assert.Null(setPropertyCall);
    }

    // ── Reconstitute behavior ───────────────────────────────────

    [Fact]
    public void Reconstitute_MapsAllFields()
    {
        var entidad = CrearEntidad(isActive: true);

        var dominio = Habilidad.Reconstitute(
            entidad.Id,
            entidad.Codigo,
            entidad.Nombre,
            entidad.Categoria,
            entidad.Descripcion,
            entidad.IsActive,
            entidad.CreatedAt,
            entidad.CreatedByUserId,
            entidad.UpdatedAt,
            entidad.UpdatedByUserId,
            entidad.IsDeleted,
            entidad.DeletedAt,
            entidad.DeletedByUserId);

        Assert.Equal(entidad.Id, dominio.Id);
        Assert.Equal("HAB-001", dominio.Codigo);
        Assert.Equal("Liderazgo", dominio.Nombre);
        Assert.Equal("Soft", dominio.Categoria);
        Assert.Equal("Capacidad de liderazgo", dominio.Descripcion);
        Assert.True(dominio.IsActive);
    }

    [Fact]
    public void Reconstitute_IsActiveFalsePreservaFlag()
    {
        var entidad = CrearEntidad(isActive: false);

        var dominio = Habilidad.Reconstitute(
            entidad.Id, entidad.Codigo, entidad.Nombre,
            entidad.Categoria, entidad.Descripcion, entidad.IsActive,
            entidad.CreatedAt, entidad.CreatedByUserId,
            entidad.UpdatedAt, entidad.UpdatedByUserId,
            entidad.IsDeleted, entidad.DeletedAt, entidad.DeletedByUserId);

        Assert.False(dominio.IsActive);
    }

    [Fact]
    public void Reconstitute_AuditFieldsPreservados()
    {
        var entidad = CrearEntidad(isActive: true);
        entidad.UpdatedAt = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        entidad.UpdatedByUserId = "user-42";

        var dominio = Habilidad.Reconstitute(
            entidad.Id, entidad.Codigo, entidad.Nombre,
            entidad.Categoria, entidad.Descripcion, entidad.IsActive,
            entidad.CreatedAt, entidad.CreatedByUserId,
            entidad.UpdatedAt, entidad.UpdatedByUserId,
            entidad.IsDeleted, entidad.DeletedAt, entidad.DeletedByUserId);

        Assert.Equal(entidad.CreatedAt, dominio.CreatedAt);
        Assert.Equal("system", dominio.CreatedByUserId);
        Assert.Equal(entidad.UpdatedAt, dominio.UpdatedAt);
        Assert.Equal("user-42", dominio.UpdatedByUserId);
    }

    [Fact]
    public void Reconstitute_CodigoVacio_LanzaArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Habilidad.Reconstitute(
                Guid.NewGuid(),
                codigo: "",
                nombre: "Cualquiera",
                categoria: null,
                descripcion: null,
                isActive: true,
                DateTime.UtcNow, null, null, null,
                false, null, null));

        Assert.Contains("Codigo", ex.Message);
    }
}