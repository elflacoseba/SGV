using System.Reflection;
using SGV.Dominio.Ocupaciones;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class OcupacionMapperTests
{
    private static OcupacionEntity CrearEntidadActiva()
    {
        return new OcupacionEntity
        {
            Id = Guid.Parse("f0000000-0000-0000-0000-000000000001"),
            PersonaId = Guid.Parse("e0000000-0000-0000-0000-000000000001"),
            PuestoId = Guid.Parse("c0000000-0000-0000-0000-000000000001"),
            FechaInicio = new DateOnly(2024, 1, 1),
            FechaFin = null,
            TipoAsignacion = TipoAsignacion.Permanente,
            Observaciones = "Test occupant",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    private static Ocupacion CrearDominioActiva()
    {
        return new Ocupacion(
            Guid.Parse("e0000000-0000-0000-0000-000000000001"),
            Guid.Parse("c0000000-0000-0000-0000-000000000001"),
            new DateOnly(2024, 1, 1),
            TipoAsignacion.Permanente,
            observaciones: "Test occupant")
        {
            Id = Guid.Parse("f0000000-0000-0000-0000-000000000001")
        };
    }

    // ── PersistenceToDomain ─────────────────────────────────────

    [Fact]
    public void MapPersistenceToDomain_Active_MapsAllFields()
    {
        var entity = CrearEntidadActiva();

        var domain = PersistenceToDomainMapper.ToDomain(entity);

        Assert.NotNull(domain);
        Assert.Equal(entity.Id, domain.Id);
        Assert.Equal(entity.PersonaId, domain.PersonaId);
        Assert.Equal(entity.PuestoId, domain.PuestoId);
        Assert.Equal(entity.FechaInicio, domain.FechaInicio);
        Assert.Equal(entity.FechaFin, domain.FechaFin);
        Assert.Equal(entity.TipoAsignacion, domain.TipoAsignacion);
        Assert.Equal(entity.Observaciones, domain.Observaciones);
        Assert.Equal(entity.IsDeleted, domain.IsDeleted);
        Assert.True(domain.EsVigente);
    }

    [Fact]
    public void MapPersistenceToDomain_Finalized_MapsFechaFinAndNotVigente()
    {
        var entity = CrearEntidadActiva();
        entity.FechaFin = new DateOnly(2024, 12, 31);

        var domain = PersistenceToDomainMapper.ToDomain(entity);

        Assert.NotNull(domain);
        Assert.Equal(new DateOnly(2024, 12, 31), domain.FechaFin);
        Assert.False(domain.EsVigente);
    }

    [Fact]
    public void MapPersistenceToDomain_Deleted_MapsIsDeletedAndNotVigente()
    {
        var entity = CrearEntidadActiva();
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        var domain = PersistenceToDomainMapper.ToDomain(entity);

        Assert.NotNull(domain);
        Assert.True(domain.IsDeleted);
        Assert.NotNull(domain.DeletedAt);
        Assert.False(domain.EsVigente);
    }

    [Fact]
    public void MapPersistenceToDomain_IncludesNavigationProperties()
    {
        var entity = CrearEntidadActiva();
        entity.Persona = new PersonaEntity
        {
            Id = entity.PersonaId,
            Nombres = "Juan",
            Apellidos = "Perez",
            Legajo = "LEG-001",
            Email = "juan@test.com",
            IsActive = true
        };
        entity.Puesto = new PuestoEntity
        {
            Id = entity.PuestoId,
            Codigo = "GER-001",
            Nombre = "Gerente General",
            UnidadOrganizativaId = Guid.NewGuid(),
            CargoId = Guid.NewGuid(),
            IsActive = true
        };

        var domain = PersistenceToDomainMapper.ToDomain(entity);

        Assert.NotNull(domain.Persona);
        Assert.Equal("Juan", domain.Persona.Nombres);
        Assert.Equal("Perez", domain.Persona.Apellidos);
        Assert.NotNull(domain.Puesto);
        Assert.Equal("Gerente General", domain.Puesto.Nombre);
    }

    // ── DomainToPersistence ─────────────────────────────────────

    [Fact]
    public void MapDomainToEntity_Active_MapsAllFields()
    {
        var domain = CrearDominioActiva();

        var entity = DomainToPersistenceMapper.ToEntity(domain);

        Assert.NotNull(entity);
        Assert.Equal(domain.Id, entity.Id);
        Assert.Equal(domain.PersonaId, entity.PersonaId);
        Assert.Equal(domain.PuestoId, entity.PuestoId);
        Assert.Equal(domain.FechaInicio, entity.FechaInicio);
        Assert.Equal(domain.FechaFin, entity.FechaFin);
        Assert.Equal(domain.TipoAsignacion, entity.TipoAsignacion);
        Assert.Equal(domain.Observaciones, entity.Observaciones);
        Assert.Equal(domain.IsDeleted, entity.IsDeleted);
        Assert.False(entity.IsDeleted);
    }

    // ── VacanteId round-trip (T-1.5) ────────────────────────────

    [Fact]
    public void MapDomainToEntity_ConVacanteId_Preserva()
    {
        var domain = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2024, 1, 1),
            TipoAsignacion.Permanente, vacanteId: Guid.NewGuid());

        var entity = DomainToPersistenceMapper.ToEntity(domain);

        Assert.Equal(domain.VacanteId, entity.VacanteId);
    }

    [Fact]
    public void MapDomainToEntity_SinVacanteId_PermiteNull()
    {
        var domain = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2024, 1, 1),
            TipoAsignacion.Permanente);

        var entity = DomainToPersistenceMapper.ToEntity(domain);

        Assert.Null(entity.VacanteId);
    }

    [Fact]
    public void MapPersistenceToDomain_ConVacanteId_HidrataPropiedad()
    {
        var vacanteId = Guid.NewGuid();
        var entity = CrearEntidadActiva();
        entity.VacanteId = vacanteId;

        var domain = PersistenceToDomainMapper.ToDomain(entity);

        Assert.Equal(vacanteId, domain.VacanteId);
    }

    [Fact]
    public void MapPersistenceToDomain_SinVacanteId_HidrataNull()
    {
        var entity = CrearEntidadActiva();
        entity.VacanteId = null;

        var domain = PersistenceToDomainMapper.ToDomain(entity);

        Assert.Null(domain.VacanteId);
    }

    [Fact]
    public void UpdateEntity_SincronizaVacanteId()
    {
        var entity = CrearEntidadActiva();
        entity.VacanteId = Guid.NewGuid();

        var vacanteAnterior = entity.VacanteId;
        var vacanteNuevo = Guid.NewGuid();

        var dominio = new Ocupacion(
            entity.PersonaId, entity.PuestoId, entity.FechaInicio,
            entity.TipoAsignacion, entity.FechaFin, entity.Observaciones,
            vacanteId: vacanteNuevo)
        {
            Id = entity.Id
        };

        DomainToPersistenceMapper.UpdateEntity(entity, dominio);

        Assert.Equal(vacanteNuevo, entity.VacanteId);
        Assert.NotEqual(vacanteAnterior, entity.VacanteId);
    }

    [Fact]
    public void MapDomainToEntity_Deleted_MapsAuditFields()
    {
        var domain = CrearDominioActiva();
        domain.EliminarLogicamente();

        var entity = DomainToPersistenceMapper.ToEntity(domain);

        Assert.True(entity.IsDeleted);
        Assert.NotNull(entity.DeletedAt);
    }

    [Fact]
    public void UpdateEntity_SyncsAllEditableFields()
    {
        var entity = CrearEntidadActiva();
        var originalId = entity.Id;
        var originalPersonaId = entity.PersonaId;
        var originalPuestoId = entity.PuestoId;

        var domain = new Ocupacion(
            Guid.Parse("e0000000-0000-0000-0000-000000000002"),
            Guid.Parse("c0000000-0000-0000-0000-000000000002"),
            new DateOnly(2024, 6, 1),
            TipoAsignacion.Interina,
            observaciones: "Updated")
        {
            Id = originalId
        };

        DomainToPersistenceMapper.UpdateEntity(entity, domain);

        Assert.Equal(originalId, entity.Id);
        Assert.Equal(Guid.Parse("e0000000-0000-0000-0000-000000000002"), entity.PersonaId);
        Assert.Equal(Guid.Parse("c0000000-0000-0000-0000-000000000002"), entity.PuestoId);
        Assert.Equal(new DateOnly(2024, 6, 1), entity.FechaInicio);
        Assert.Equal(TipoAsignacion.Interina, entity.TipoAsignacion);
        Assert.Equal("Updated", entity.Observaciones);
    }

    [Fact]
    public void UpdateEntity_WithFinalize_SyncsFechaFin()
    {
        var entity = CrearEntidadActiva();
        var domain = PersistenceToDomainMapper.ToDomain(entity);

        domain.Finalizar(new DateOnly(2024, 12, 31));
        DomainToPersistenceMapper.UpdateEntity(entity, domain);

        Assert.Equal(new DateOnly(2024, 12, 31), entity.FechaFin);
    }

    // ── Mapper reflection guard (issue #124) ─────────────────────

    [Fact]
    public void ToDomain_Ocupacion_NoLlamaSetPropertyReflectionHelper()
    {
        var assembly = typeof(OcupacionRepository).Assembly;
        var mapperType = assembly.GetType(
            "SGV.Infraestructura.Persistencia.Mapeos.PersistenceToDomainMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            "ToDomain",
            new[] { typeof(OcupacionEntity) })
            ?? throw new InvalidOperationException(
                "PersistenceToDomainMapper.ToDomain(OcupacionEntity) not found.");
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

    // ── Reconstitute behavior (issue #124) ───────────────────────

    [Fact]
    public void Reconstitute_MapsAllFields()
    {
        var fechaInicio = new DateOnly(2024, 1, 1);
        var fechaFin = new DateOnly(2024, 12, 31);

        var dominio = Ocupacion.Reconstitute(
            id: Guid.NewGuid(),
            personaId: Guid.NewGuid(),
            puestoId: Guid.NewGuid(),
            fechaInicio: fechaInicio,
            fechaFin: fechaFin,
            tipoAsignacion: TipoAsignacion.Permanente,
            observaciones: "Obs",
            persona: null,
            puesto: null,
            vacanteId: null,
            vacante: null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.Equal(fechaInicio, dominio.FechaInicio);
        Assert.Equal(fechaFin, dominio.FechaFin);
        Assert.Equal(TipoAsignacion.Permanente, dominio.TipoAsignacion);
        Assert.Equal("Obs", dominio.Observaciones);
        Assert.Null(dominio.VacanteId);
        Assert.False(dominio.EsVigente);
    }

    [Fact]
    public void Reconstitute_FechaFinBeforeFechaInicio_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Ocupacion.Reconstitute(
                id: Guid.NewGuid(),
                personaId: Guid.NewGuid(),
                puestoId: Guid.NewGuid(),
                fechaInicio: new DateOnly(2024, 6, 1),
                fechaFin: new DateOnly(2024, 1, 1),
                tipoAsignacion: TipoAsignacion.Interina,
                observaciones: null,
                persona: null,
                puesto: null,
                vacanteId: null,
                vacante: null,
                DateTime.UtcNow, null, null, null, false, null, null));
    }

    [Fact]
    public void Reconstitute_EsVigenteTrueSinFechaFin()
    {
        var dominio = Ocupacion.Reconstitute(
            id: Guid.NewGuid(),
            personaId: Guid.NewGuid(),
            puestoId: Guid.NewGuid(),
            fechaInicio: new DateOnly(2024, 1, 1),
            fechaFin: null,
            tipoAsignacion: TipoAsignacion.Permanente,
            observaciones: null,
            persona: null,
            puesto: null,
            vacanteId: null,
            vacante: null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.True(dominio.EsVigente);
    }

    [Fact]
    public void Reconstitute_EsVigenteFalseConFechaFin()
    {
        var dominio = Ocupacion.Reconstitute(
            id: Guid.NewGuid(),
            personaId: Guid.NewGuid(),
            puestoId: Guid.NewGuid(),
            fechaInicio: new DateOnly(2024, 1, 1),
            fechaFin: new DateOnly(2024, 12, 31),
            tipoAsignacion: TipoAsignacion.Permanente,
            observaciones: null,
            persona: null,
            puesto: null,
            vacanteId: null,
            vacante: null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.False(dominio.EsVigente);
    }
}
