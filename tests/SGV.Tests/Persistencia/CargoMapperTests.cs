using System.Reflection;
using SGV.Dominio.Habilidades;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Reflection guard + behavior coverage for the Cargo persistence mapper.
/// See issue #124: <c>ToDomain(CargoEntity)</c> must not call the internal
/// <c>SetProperty</c> reflection helper; instead it should delegate to
/// <c>Cargo.Reconstitute(...)</c>.
/// </summary>
public sealed class CargoMapperTests
{
    private static readonly Guid Id = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid NivelId = Guid.Parse("b0000000-0000-0000-0000-000000000002");

    private static CargoEntity CrearEntidad(bool isActive, bool isDeleted = false)
    {
        return new CargoEntity
        {
            Id = Id,
            Codigo = "CAR-001",
            Nombre = "Gerente General",
            NivelId = NivelId,
            Descripcion = "Cargo de prueba",
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = "system"
        };
    }

    // ── IL reflection guard ─────────────────────────────────────

    [Fact]
    public void ToDomain_Cargo_NoLlamaSetPropertyReflectionHelper()
    {
        var assembly = typeof(CargoRepository).Assembly;
        var mapperType = assembly.GetType(
            "SGV.Infraestructura.Persistencia.Mapeos.PersistenceToDomainMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            "ToDomain",
            new[] { typeof(CargoEntity) })
            ?? throw new InvalidOperationException(
                "PersistenceToDomainMapper.ToDomain(CargoEntity) not found.");
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

        var dominio = Cargo.Reconstitute(
            entidad.Id,
            entidad.Codigo,
            entidad.Nombre,
            entidad.NivelId,
            entidad.Descripcion,
            entidad.IsActive,
            nivelCargo: null,
            entidad.CreatedAt,
            entidad.CreatedByUserId,
            entidad.UpdatedAt,
            entidad.UpdatedByUserId,
            entidad.IsDeleted,
            entidad.DeletedAt,
            entidad.DeletedByUserId);

        Assert.Equal(entidad.Id, dominio.Id);
        Assert.Equal("CAR-001", dominio.Codigo);
        Assert.Equal("Gerente General", dominio.Nombre);
        Assert.Equal(NivelId, dominio.NivelId);
        Assert.Equal("Cargo de prueba", dominio.Descripcion);
        Assert.True(dominio.IsActive);
        Assert.Null(dominio.NivelCargo);
    }

    [Fact]
    public void Reconstitute_IsActiveFalseNoDisparaValidacion()
    {
        // Cargo.Desactivar() lanza InvalidOperationException si hay puestos
        // activos subordinados. Reconstitute hidrata el flag sin disparar
        // esa validación (es un factory de lectura, no una transición).
        var entidad = CrearEntidad(isActive: false);

        var dominio = Cargo.Reconstitute(
            entidad.Id, entidad.Codigo, entidad.Nombre,
            entidad.NivelId, entidad.Descripcion, entidad.IsActive,
            nivelCargo: null,
            entidad.CreatedAt, entidad.CreatedByUserId,
            entidad.UpdatedAt, entidad.UpdatedByUserId,
            entidad.IsDeleted, entidad.DeletedAt, entidad.DeletedByUserId);

        Assert.False(dominio.IsActive);
    }

    [Fact]
    public void Reconstitute_NivelCargoNull()
    {
        var dominio = Cargo.Reconstitute(
            Id, "CAR-001", "Gerente", NivelId, null, true,
            nivelCargo: null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.Null(dominio.NivelCargo);
    }

    [Fact]
    public void Reconstitute_NivelCargoHydrated()
    {
        var nivel = new NivelCargo("NIV-1", "Nivel 1", 1, 1);

        var dominio = Cargo.Reconstitute(
            Id, "CAR-001", "Gerente", NivelId, null, true,
            nivelCargo: nivel,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.NotNull(dominio.NivelCargo);
        Assert.Same(nivel, dominio.NivelCargo);
    }

    [Fact]
    public void Reconstitute_NivelIdVacio_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Cargo.Reconstitute(
                Guid.NewGuid(),
                codigo: "CAR-001",
                nombre: "Cualquiera",
                nivelId: Guid.Empty,
                descripcion: null,
                isActive: true,
                nivelCargo: null,
                DateTime.UtcNow, null, null, null, false, null, null));
    }
}