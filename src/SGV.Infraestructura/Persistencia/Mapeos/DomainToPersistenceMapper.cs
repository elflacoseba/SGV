using SGV.Dominio.Habilidades;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Mapeos;

/// <summary>
/// Maps domain instances to persistence entities for write operations.
/// </summary>
internal static class DomainToPersistenceMapper
{
    public static UnidadOrganizativaEntity ToEntity(UnidadOrganizativa domain)
    {
        return new UnidadOrganizativaEntity
        {
            Id = domain.Id,
            Codigo = domain.Codigo,
            Nombre = domain.Nombre,
            TipoUnidadOrganizativaId = domain.TipoUnidadOrganizativaId,
            Descripcion = domain.Descripcion,
            VigenteDesde = domain.VigenteDesde,
            VigenteHasta = domain.VigenteHasta,
            IsActive = domain.IsActive,
            IsDeleted = domain.IsDeleted,
            UnidadPadreId = domain.UnidadPadreId,
            CreatedAt = domain.CreatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedAt = domain.UpdatedAt,
            UpdatedByUserId = domain.UpdatedByUserId,
            DeletedAt = domain.DeletedAt,
            DeletedByUserId = domain.DeletedByUserId
        };
    }

    public static void UpdateEntity(UnidadOrganizativaEntity entity, UnidadOrganizativa domain)
    {
        entity.Codigo = domain.Codigo;
        entity.Nombre = domain.Nombre;
        entity.TipoUnidadOrganizativaId = domain.TipoUnidadOrganizativaId;
        entity.Descripcion = domain.Descripcion;
        entity.VigenteDesde = domain.VigenteDesde;
        entity.VigenteHasta = domain.VigenteHasta;
        entity.IsActive = domain.IsActive;
        entity.IsDeleted = domain.IsDeleted;
        entity.UnidadPadreId = domain.UnidadPadreId;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedByUserId = domain.UpdatedByUserId;
        entity.DeletedAt = domain.DeletedAt;
        entity.DeletedByUserId = domain.DeletedByUserId;
    }

    public static CargoEntity ToEntity(Cargo domain)
    {
        return new CargoEntity
        {
            Id = domain.Id,
            Codigo = domain.Codigo,
            Nombre = domain.Nombre,
            NivelId = domain.NivelId,
            Descripcion = domain.Descripcion,
            IsActive = domain.IsActive,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedAt = domain.UpdatedAt,
            UpdatedByUserId = domain.UpdatedByUserId,
            DeletedAt = domain.DeletedAt,
            DeletedByUserId = domain.DeletedByUserId
        };
    }

    public static void UpdateEntity(CargoEntity entity, Cargo domain)
    {
        entity.Codigo = domain.Codigo;
        entity.Nombre = domain.Nombre;
        entity.NivelId = domain.NivelId;
        entity.Descripcion = domain.Descripcion;
        entity.IsActive = domain.IsActive;
        entity.IsDeleted = domain.IsDeleted;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedByUserId = domain.UpdatedByUserId;
        entity.DeletedAt = domain.DeletedAt;
        entity.DeletedByUserId = domain.DeletedByUserId;
    }

    public static PuestoEntity ToEntity(Puesto domain)
    {
        return new PuestoEntity
        {
            Id = domain.Id,
            Codigo = domain.Codigo,
            Nombre = domain.Nombre,
            Descripcion = domain.Descripcion,
            UnidadOrganizativaId = domain.UnidadOrganizativaId,
            CargoId = domain.CargoId,
            PuestoSuperiorId = domain.PuestoSuperiorId,
            IsActive = domain.IsActive,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedAt = domain.UpdatedAt,
            UpdatedByUserId = domain.UpdatedByUserId,
            DeletedAt = domain.DeletedAt,
            DeletedByUserId = domain.DeletedByUserId
        };
    }

    public static void UpdateEntity(PuestoEntity entity, Puesto domain)
    {
        entity.Codigo = domain.Codigo;
        entity.Nombre = domain.Nombre;
        entity.Descripcion = domain.Descripcion;
        entity.UnidadOrganizativaId = domain.UnidadOrganizativaId;
        entity.CargoId = domain.CargoId;
        entity.PuestoSuperiorId = domain.PuestoSuperiorId;
        entity.IsActive = domain.IsActive;
        entity.IsDeleted = domain.IsDeleted;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedByUserId = domain.UpdatedByUserId;
        entity.DeletedAt = domain.DeletedAt;
        entity.DeletedByUserId = domain.DeletedByUserId;
    }

    public static HabilidadEntity ToEntity(Habilidad domain)
    {
        return new HabilidadEntity
        {
            Id = domain.Id,
            Codigo = domain.Codigo,
            Nombre = domain.Nombre,
            Descripcion = domain.Descripcion,
            Categoria = domain.Categoria,
            IsActive = domain.IsActive,
            CreatedAt = domain.CreatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedAt = domain.UpdatedAt,
            UpdatedByUserId = domain.UpdatedByUserId
        };
    }

    public static void UpdateEntity(HabilidadEntity entity, Habilidad domain)
    {
        entity.Nombre = domain.Nombre;
        entity.Descripcion = domain.Descripcion;
        entity.Categoria = domain.Categoria;
        entity.IsActive = domain.IsActive;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedByUserId = domain.UpdatedByUserId;
    }

    public static CargoHabilidadEntity ToEntity(CargoHabilidad domain)
    {
        return new CargoHabilidadEntity
        {
            Id = domain.Id,
            CargoId = domain.CargoId,
            HabilidadId = domain.HabilidadId,
            NivelRequeridoId = domain.NivelRequeridoId,
            Ponderacion = domain.Ponderacion,
            EsObligatoria = domain.EsObligatoria
        };
    }

    public static PersonaHabilidadEntity ToEntity(PersonaHabilidad domain)
    {
        return new PersonaHabilidadEntity
        {
            Id = domain.Id,
            PersonaId = domain.PersonaId,
            HabilidadId = domain.HabilidadId,
            NivelHabilidadId = domain.NivelHabilidadId,
            VerificadoAt = domain.VerificadoAt,
            Fuente = domain.Fuente
        };
    }

    public static NivelCargoEntity ToEntity(NivelCargo domain)
    {
        return new NivelCargoEntity
        {
            Id = domain.Id,
            Codigo = domain.Codigo,
            Nombre = domain.Nombre,
            ValorNumerico = domain.ValorNumerico,
            Orden = domain.Orden
        };
    }

    public static PersonaEntity ToEntity(Persona domain)
    {
        return new PersonaEntity
        {
            Id = domain.Id,
            Legajo = domain.Legajo,
            Nombres = domain.Nombres,
            Apellidos = domain.Apellidos,
            Email = domain.Email,
            TipoDocumento = domain.TipoDocumento,
            NumeroDocumento = domain.NumeroDocumento,
            Telefono = domain.Telefono,
            IsActive = domain.IsActive,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedAt = domain.UpdatedAt,
            UpdatedByUserId = domain.UpdatedByUserId,
            DeletedAt = domain.DeletedAt,
            DeletedByUserId = domain.DeletedByUserId
        };
    }

    public static void UpdateEntity(PersonaEntity entity, Persona domain)
    {
        entity.Legajo = domain.Legajo;
        entity.Nombres = domain.Nombres;
        entity.Apellidos = domain.Apellidos;
        entity.Email = domain.Email;
        entity.TipoDocumento = domain.TipoDocumento;
        entity.NumeroDocumento = domain.NumeroDocumento;
        entity.Telefono = domain.Telefono;
        entity.IsActive = domain.IsActive;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedByUserId = domain.UpdatedByUserId;
        entity.DeletedAt = domain.DeletedAt;
        entity.DeletedByUserId = domain.DeletedByUserId;
    }
}
