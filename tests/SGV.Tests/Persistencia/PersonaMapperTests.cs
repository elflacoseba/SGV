using System.Reflection;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Reflection guard + behavior coverage for the Persona persistence mapper.
/// See issue #124: <c>ToDomain(PersonaEntity)</c> must not call the internal
/// <c>SetProperty</c> reflection helper; instead it should delegate to
/// <c>Persona.Reconstitute(...)</c>.
/// </summary>
public sealed class PersonaMapperTests
{
    private static readonly Guid Id = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private static PersonaEntity CrearEntidad(bool isActive, bool isDeleted = false)
    {
        return new PersonaEntity
        {
            Id = Id,
            Nombres = "Juan",
            Apellidos = "Perez",
            Legajo = "LEG-001",
            Email = "juan@test.com",
            // Issue #147: TipoDocumentoId reemplaza al string TipoDocumento.
            TipoDocumentoId = new Guid("11111111-1111-1111-1111-111111111111"),
            NumeroDocumento = "12345678",
            Telefono = "+54911223344",
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = "system"
        };
    }

    // ── IL reflection guard ─────────────────────────────────────

    [Fact]
    public void ToDomain_Persona_NoLlamaSetPropertyReflectionHelper()
    {
        var assembly = typeof(PersonaRepository).Assembly;
        var mapperType = assembly.GetType(
            "SGV.Infraestructura.Persistencia.Mapeos.PersistenceToDomainMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            "ToDomain",
            new[] { typeof(PersonaEntity) })
            ?? throw new InvalidOperationException(
                "PersistenceToDomainMapper.ToDomain(PersonaEntity) not found.");
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

        var dominio = Persona.Reconstitute(
            entidad.Id,
            entidad.Nombres,
            entidad.Apellidos,
            entidad.Legajo,
            entidad.Email,
            entidad.TipoDocumentoId,
            entidad.NumeroDocumento,
            entidad.Telefono,
            entidad.IsActive,
            entidad.CreatedAt,
            entidad.CreatedByUserId,
            entidad.UpdatedAt,
            entidad.UpdatedByUserId,
            entidad.IsDeleted,
            entidad.DeletedAt,
            entidad.DeletedByUserId);

        Assert.Equal(entidad.Id, dominio.Id);
        Assert.Equal("Juan", dominio.Nombres);
        Assert.Equal("Perez", dominio.Apellidos);
        Assert.Equal("LEG-001", dominio.Legajo);
        Assert.Equal("juan@test.com", dominio.Email);
        Assert.Equal(entidad.TipoDocumentoId, dominio.TipoDocumentoId);
        Assert.True(dominio.IsActive);
    }

    [Fact]
    public void Reconstitute_MapsAllDocumentFields()
    {
        var entidad = CrearEntidad(isActive: true);

        var dominio = Persona.Reconstitute(
            entidad.Id, entidad.Nombres, entidad.Apellidos,
            entidad.Legajo, entidad.Email,
            entidad.TipoDocumentoId, entidad.NumeroDocumento, entidad.Telefono,
            entidad.IsActive,
            entidad.CreatedAt, entidad.CreatedByUserId,
            entidad.UpdatedAt, entidad.UpdatedByUserId,
            entidad.IsDeleted, entidad.DeletedAt, entidad.DeletedByUserId);

        Assert.Equal(entidad.TipoDocumentoId, dominio.TipoDocumentoId);
        Assert.Equal("12345678", dominio.NumeroDocumento);
    }

    [Fact]
    public void Reconstitute_TelefonoAsignado()
    {
        var entidad = CrearEntidad(isActive: true);

        var dominio = Persona.Reconstitute(
            entidad.Id, entidad.Nombres, entidad.Apellidos,
            entidad.Legajo, entidad.Email,
            entidad.TipoDocumentoId, entidad.NumeroDocumento, entidad.Telefono,
            entidad.IsActive,
            entidad.CreatedAt, entidad.CreatedByUserId,
            entidad.UpdatedAt, entidad.UpdatedByUserId,
            entidad.IsDeleted, entidad.DeletedAt, entidad.DeletedByUserId);

        Assert.Equal("+54911223344", dominio.Telefono);
    }

    [Fact]
    public void Reconstitute_IsActiveFalsePreservaFlag()
    {
        var entidad = CrearEntidad(isActive: false);

        var dominio = Persona.Reconstitute(
            entidad.Id, entidad.Nombres, entidad.Apellidos,
            entidad.Legajo, entidad.Email,
            entidad.TipoDocumentoId, entidad.NumeroDocumento, entidad.Telefono,
            entidad.IsActive,
            entidad.CreatedAt, entidad.CreatedByUserId,
            entidad.UpdatedAt, entidad.UpdatedByUserId,
            entidad.IsDeleted, entidad.DeletedAt, entidad.DeletedByUserId);

        Assert.False(dominio.IsActive);
    }

    [Fact]
    public void Reconstitute_AuditFieldsPreservados()
    {
        var entidad = CrearEntidad(isActive: true);
        entidad.UpdatedAt = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        entidad.UpdatedByUserId = "user-42";

        var dominio = Persona.Reconstitute(
            entidad.Id, entidad.Nombres, entidad.Apellidos,
            entidad.Legajo, entidad.Email,
            entidad.TipoDocumentoId, entidad.NumeroDocumento, entidad.Telefono,
            entidad.IsActive,
            entidad.CreatedAt, entidad.CreatedByUserId,
            entidad.UpdatedAt, entidad.UpdatedByUserId,
            entidad.IsDeleted, entidad.DeletedAt, entidad.DeletedByUserId);

        Assert.Equal(entidad.CreatedAt, dominio.CreatedAt);
        Assert.Equal("system", dominio.CreatedByUserId);
        Assert.Equal(entidad.UpdatedAt, dominio.UpdatedAt);
        Assert.Equal("user-42", dominio.UpdatedByUserId);
    }

    [Fact]
    public void Reconstitute_NombresVacio_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Persona.Reconstitute(
                Guid.NewGuid(),
                nombres: "",
                apellidos: "Perez",
                legajo: null,
                email: null,
                tipoDocumentoId: null,
                numeroDocumento: null,
                telefono: null,
                isActive: true,
                DateTime.UtcNow, null, null, null, false, null, null));
    }
}