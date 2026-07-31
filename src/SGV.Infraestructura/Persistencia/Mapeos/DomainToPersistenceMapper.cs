using SGV.Dominio.Habilidades;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Personas;
using SGV.Dominio.Vacantes;
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
            CategoriaId = domain.CategoriaId,
            IsActive = domain.IsActive,
            CreatedAt = domain.CreatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedAt = domain.UpdatedAt,
            UpdatedByUserId = domain.UpdatedByUserId
        };
    }

    public static void UpdateEntity(HabilidadEntity entity, Habilidad domain)
    {
        entity.Codigo = domain.Codigo;
        entity.Nombre = domain.Nombre;
        entity.Descripcion = domain.Descripcion;
        entity.CategoriaId = domain.CategoriaId;
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
            TipoDocumentoId = domain.TipoDocumentoId,
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
        entity.TipoDocumentoId = domain.TipoDocumentoId;
        entity.NumeroDocumento = domain.NumeroDocumento;
        entity.Telefono = domain.Telefono;
        entity.IsActive = domain.IsActive;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedByUserId = domain.UpdatedByUserId;
        entity.DeletedAt = domain.DeletedAt;
        entity.DeletedByUserId = domain.DeletedByUserId;
    }

    public static OcupacionEntity ToEntity(Ocupacion domain)
    {
        return new OcupacionEntity
        {
            Id = domain.Id,
            PersonaId = domain.PersonaId,
            PuestoId = domain.PuestoId,
            FechaInicio = domain.FechaInicio,
            FechaFin = domain.FechaFin,
            TipoAsignacion = domain.TipoAsignacion,
            Observaciones = domain.Observaciones,
            CreatedAt = domain.CreatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedAt = domain.UpdatedAt,
            UpdatedByUserId = domain.UpdatedByUserId,
            IsDeleted = domain.IsDeleted,
            DeletedAt = domain.DeletedAt,
            DeletedByUserId = domain.DeletedByUserId
        };
    }

    public static void UpdateEntity(OcupacionEntity entity, Ocupacion domain)
    {
        entity.PersonaId = domain.PersonaId;
        entity.PuestoId = domain.PuestoId;
        entity.FechaInicio = domain.FechaInicio;
        entity.FechaFin = domain.FechaFin;
        entity.TipoAsignacion = domain.TipoAsignacion;
        entity.Observaciones = domain.Observaciones;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedByUserId = domain.UpdatedByUserId;
        entity.IsDeleted = domain.IsDeleted;
        entity.DeletedAt = domain.DeletedAt;
        entity.DeletedByUserId = domain.DeletedByUserId;
    }

    public static VacanteEntity ToEntity(Vacante domain)
    {
        return new VacanteEntity
        {
            Id = domain.Id,
            PuestoId = domain.PuestoId,
            EstadoVacanteId = domain.EstadoVacanteId,
            FechaApertura = domain.FechaApertura,
            FechaCierre = domain.FechaCierre,
            Motivo = domain.Motivo,
            Observaciones = domain.Observaciones,
            CreatedAt = domain.CreatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedAt = domain.UpdatedAt,
            UpdatedByUserId = domain.UpdatedByUserId,
            IsDeleted = domain.IsDeleted,
            DeletedAt = domain.DeletedAt,
            DeletedByUserId = domain.DeletedByUserId
        };
    }

    public static void UpdateEntity(VacanteEntity entity, Vacante domain)
    {
        entity.PuestoId = domain.PuestoId;
        entity.EstadoVacanteId = domain.EstadoVacanteId;
        entity.FechaApertura = domain.FechaApertura;
        entity.FechaCierre = domain.FechaCierre;
        entity.Motivo = domain.Motivo;
        entity.Observaciones = domain.Observaciones;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedByUserId = domain.UpdatedByUserId;
        entity.IsDeleted = domain.IsDeleted;
        entity.DeletedAt = domain.DeletedAt;
        entity.DeletedByUserId = domain.DeletedByUserId;
    }

    /// <summary>
    /// Construye la entidad de persistencia de un historial de estado de
    /// vacante a partir del dominio. Usado por
    /// <c>VacanteRepository.RegistrarCambioEstadoAsync</c> cuando el
    /// servicio <c>VacanteServicioComandos.CambiarEstadoAsync</c> emite
    /// un nuevo <see cref="HistorialEstadoVacante"/> tras la mutación del
    /// agregado. El <c>Id</c> se genera nuevo porque el dominio crea el
    /// historial sin un ID explícito (la PK es autogenerada por EF).
    /// </summary>
    public static HistorialEstadoVacanteEntity ToEntity(HistorialEstadoVacante domain)
    {
        return new HistorialEstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            VacanteId = domain.VacanteId,
            EstadoAnteriorId = domain.EstadoAnteriorId,
            EstadoNuevoId = domain.EstadoNuevoId,
            ChangedAt = domain.ChangedAt,
            ChangedByUserId = domain.ChangedByUserId,
            Motivo = domain.Motivo
        };
    }
}
