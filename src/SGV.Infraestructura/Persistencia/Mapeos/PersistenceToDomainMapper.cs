using SGV.Dominio.Comun;
using SGV.Dominio.Habilidades;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Mapeos;

/// <summary>
/// Mapeos explícitos de entidades de persistencia a entidades del Dominio
/// para preservar el contrato actual de los repositorios.
/// </summary>
internal static class PersistenceToDomainMapper
{
    public static Cargo ToDomain(CargoEntity entity)
    {
        return Cargo.Reconstitute(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.NivelId,
            entity.Descripcion,
            entity.IsActive,
            entity.NivelCargo is null ? null : ToDomain(entity.NivelCargo),
            entity.CreatedAt,
            entity.CreatedByUserId,
            entity.UpdatedAt,
            entity.UpdatedByUserId,
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedByUserId);
    }

    public static NivelCargo ToDomain(NivelCargoEntity entity)
    {
        var nivel = new NivelCargo(entity.Codigo, entity.Nombre, entity.ValorNumerico, entity.Orden)
        {
            Id = entity.Id
        };
        return nivel;
    }

    public static TipoDocumento ToDomain(TipoDocumentoEntity entity)
    {
        var tipo = new TipoDocumento(
            entity.Codigo,
            entity.Nombre,
            entity.PatronValidacion,
            entity.LongitudMinima,
            entity.LongitudMaxima)
        {
            Id = entity.Id
        };
        return tipo;
    }

    public static Habilidad ToDomain(HabilidadEntity entity)
    {
        return Habilidad.Reconstitute(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.CategoriaId,
            entity.Descripcion,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedByUserId,
            entity.UpdatedAt,
            entity.UpdatedByUserId,
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedByUserId);
    }

    public static CategoriaHabilidad ToDomain(CategoriaHabilidadEntity entity)
    {
        return CategoriaHabilidad.Reconstitute(entity.Id, entity.Codigo, entity.Nombre);
    }

    public static UnidadOrganizativa ToDomain(UnidadOrganizativaEntity entity)
    {
        // Issue #124: la hidratación se hace vía Reconstitute en lugar de `with`,
        // para mantener un único patrón con las otras 5 entidades y eliminar la
        // dependencia del record `init` (las propiedades de UO migraron a
        // `private set` para paridad). `Codigo` sigue asignándose solo en el
        // constructor (o en el factory Reconstitute, que es interno).
        return UnidadOrganizativa.Reconstitute(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.TipoUnidadOrganizativaId,
            entity.Descripcion,
            entity.UnidadPadreId,
            entity.VigenteDesde,
            entity.VigenteHasta,
            entity.IsActive,
            entity.UnidadPadre is null ? null : ToDomain(entity.UnidadPadre),
            entity.TipoUnidadOrganizativa is null ? null : ToDomain(entity.TipoUnidadOrganizativa),
            entity.CreatedAt,
            entity.CreatedByUserId,
            entity.UpdatedAt,
            entity.UpdatedByUserId,
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedByUserId);
    }

    public static Puesto ToDomain(PuestoEntity entity)
    {
        return Puesto.Reconstitute(
            entity.Id,
            entity.UnidadOrganizativaId,
            entity.CargoId,
            entity.PuestoSuperiorId,
            entity.Codigo,
            entity.Nombre,
            entity.Descripcion,
            entity.IsActive,
            entity.UnidadOrganizativa is null ? null : ToDomain(entity.UnidadOrganizativa),
            entity.Cargo is null ? null : ToDomain(entity.Cargo),
            entity.CreatedAt,
            entity.CreatedByUserId,
            entity.UpdatedAt,
            entity.UpdatedByUserId,
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedByUserId);
    }

    public static TipoUnidadOrganizativa ToDomain(TipoUnidadOrganizativaEntity entity)
    {
        var tipo = new TipoUnidadOrganizativa(entity.Codigo, entity.Nombre)
        {
            Id = entity.Id
        };
        return tipo;
    }

    public static NivelHabilidad ToDomain(NivelHabilidadEntity entity)
    {
        var nivel = new NivelHabilidad(entity.Codigo, entity.Nombre, entity.ValorNumerico, entity.Orden)
        {
            Id = entity.Id
        };
        return nivel;
    }

    public static CargoHabilidad ToDomain(CargoHabilidadEntity entity)
    {
        var ch = new CargoHabilidad(entity.CargoId, entity.HabilidadId, entity.NivelRequeridoId, entity.Ponderacion, entity.EsObligatoria)
        {
            Id = entity.Id
        };
        return ch;
    }

    public static PersonaHabilidad ToDomain(PersonaHabilidadEntity entity)
    {
        var ph = new PersonaHabilidad(entity.PersonaId, entity.HabilidadId, entity.NivelHabilidadId, entity.VerificadoAt, entity.Fuente)
        {
            Id = entity.Id
        };
        return ph;
    }

    public static Persona ToDomain(PersonaEntity entity)
    {
        return Persona.Reconstitute(
            entity.Id,
            entity.Nombres,
            entity.Apellidos,
            entity.Legajo,
            entity.Email,
            entity.TipoDocumentoId,
            entity.NumeroDocumento,
            entity.Telefono,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedByUserId,
            entity.UpdatedAt,
            entity.UpdatedByUserId,
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedByUserId);
    }

    public static Ocupacion ToDomain(OcupacionEntity entity)
    {
        return Ocupacion.Reconstitute(
            entity.Id,
            entity.PersonaId,
            entity.PuestoId,
            entity.FechaInicio,
            entity.FechaFin,
            entity.TipoAsignacion,
            entity.Observaciones,
            entity.Persona is null ? null : ToDomain(entity.Persona),
            entity.Puesto is null ? null : ToDomain(entity.Puesto),
            entity.CreatedAt,
            entity.CreatedByUserId,
entity.UpdatedAt,
             entity.UpdatedByUserId,
             entity.IsDeleted,
             entity.DeletedAt,
             entity.DeletedByUserId);
    }
}
