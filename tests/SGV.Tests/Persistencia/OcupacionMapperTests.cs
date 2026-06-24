using SGV.Dominio.Ocupaciones;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;
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
}
