using System.Reflection;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Reflection guard + behavior coverage for the Puesto persistence mapper.
/// See issue #124: <c>ToDomain(PuestoEntity)</c> must not call the internal
/// <c>SetProperty</c> reflection helper; instead it should delegate to
/// <c>Puesto.Reconstitute(...)</c>.
/// </summary>
public sealed class PuestoMapperTests
{
    private static readonly Guid Id = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid UnidadId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid CargoId = Guid.Parse("c0000000-0000-0000-0000-000000000003");

    private static PuestoEntity CrearEntidad(bool isActive, bool isDeleted = false)
    {
        return new PuestoEntity
        {
            Id = Id,
            UnidadOrganizativaId = UnidadId,
            CargoId = CargoId,
            PuestoSuperiorId = null,
            Codigo = "PUE-001",
            Nombre = "Gerente",
            Descripcion = "Puesto de prueba",
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = "system"
        };
    }

    // ── IL reflection guard ─────────────────────────────────────

    [Fact]
    public void ToDomain_Puesto_NoLlamaSetPropertyReflectionHelper()
    {
        var assembly = typeof(PuestoRepository).Assembly;
        var mapperType = assembly.GetType(
            "SGV.Infraestructura.Persistencia.Mapeos.PersistenceToDomainMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            "ToDomain",
            new[] { typeof(PuestoEntity) })
            ?? throw new InvalidOperationException(
                "PersistenceToDomainMapper.ToDomain(PuestoEntity) not found.");
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
                // Token may resolve to a field reference rather than a method.
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

        var dominio = Puesto.Reconstitute(
            entidad.Id,
            entidad.UnidadOrganizativaId,
            entidad.CargoId,
            entidad.PuestoSuperiorId,
            entidad.Codigo,
            entidad.Nombre,
            entidad.Descripcion,
            entidad.IsActive,
            unidadOrganizativa: null,
            cargo: null,
            entidad.CreatedAt,
            entidad.CreatedByUserId,
            entidad.UpdatedAt,
            entidad.UpdatedByUserId,
            entidad.IsDeleted,
            entidad.DeletedAt,
            entidad.DeletedByUserId);

        Assert.Equal(entidad.Id, dominio.Id);
        Assert.Equal(UnidadId, dominio.UnidadOrganizativaId);
        Assert.Equal(CargoId, dominio.CargoId);
        Assert.Equal("PUE-001", dominio.Codigo);
        Assert.Equal("Gerente", dominio.Nombre);
        Assert.True(dominio.IsActive);
    }

    [Fact]
    public void Reconstitute_UnidadOrganizativaNavNull()
    {
        var dominio = Puesto.Reconstitute(
            Guid.NewGuid(), UnidadId, CargoId, null,
            "PUE-001", "Gerente", null, true,
            unidadOrganizativa: null, cargo: null,
            DateTime.UtcNow, null, null, null, false, null, null);

        // UnidadOrganizativa es no-nullable en el record, pero se asigna null!
        // porque la nav es opcional. La nulabilidad se respeta con ! en Reconstitute.
        Assert.Null(dominio.UnidadOrganizativa);
    }

    [Fact]
    public void Reconstitute_CargoNavNull()
    {
        var dominio = Puesto.Reconstitute(
            Guid.NewGuid(), UnidadId, CargoId, null,
            "PUE-001", "Gerente", null, true,
            unidadOrganizativa: null, cargo: null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.Null(dominio.Cargo);
    }

    [Fact]
    public void Reconstitute_IsActiveFalsePreservaFlag()
    {
        var dominio = Puesto.Reconstitute(
            Id, UnidadId, CargoId, null,
            "PUE-001", "Gerente", null, false,
            unidadOrganizativa: null, cargo: null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.False(dominio.IsActive);
    }

    [Fact]
    public void Reconstitute_PuestoSuperiorIgualId_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Puesto.Reconstitute(
                id: Id,
                unidadOrganizativaId: UnidadId,
                cargoId: CargoId,
                puestoSuperiorId: Id,
                codigo: "PUE-001",
                nombre: "Gerente",
                descripcion: null,
                isActive: true,
                unidadOrganizativa: null,
                cargo: null,
                DateTime.UtcNow, null, null, null, false, null, null));
    }
}