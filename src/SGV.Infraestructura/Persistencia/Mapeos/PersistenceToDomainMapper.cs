using System.Reflection;
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

    public static Habilidad ToDomain(HabilidadEntity entity)
    {
        return Habilidad.Reconstitute(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.Categoria,
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

    public static UnidadOrganizativa ToDomain(UnidadOrganizativaEntity entity)
    {
        // PR2: persistencia de la inmutabilidad del record. `Codigo` solo se
        // asigna en el constructor primario. Toda mutacion posterior se hace
        // con `with` para que las propiedades `init`-only de UnidadOrganizativa
        // se respeten sin recurrir a `SetProperty` + `BindingFlags.NonPublic`
        // (que evitarian el chequeo del modifier `IsExternalInit` en runtime).
        var unidad = new UnidadOrganizativa(
            entity.Codigo,
            entity.Nombre,
            entity.TipoUnidadOrganizativaId,
            entity.Descripcion,
            entity.UnidadPadreId)
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedByUserId = entity.CreatedByUserId,
            UpdatedAt = entity.UpdatedAt,
            UpdatedByUserId = entity.UpdatedByUserId,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt,
            DeletedByUserId = entity.DeletedByUserId
        };

        if (entity.UnidadPadre is not null)
        {
            unidad = unidad with { UnidadPadre = ToDomain(entity.UnidadPadre) };
        }

        if (entity.TipoUnidadOrganizativa is not null)
        {
            unidad = unidad with { TipoUnidadOrganizativa = ToDomain(entity.TipoUnidadOrganizativa) };
        }

        // DefinirVigencia valida el rango (lanza si VigenteHasta < VigenteDesde)
        // y devuelve una nueva instancia via `with`. Encadenamos otro `with`
        // para fijar IsActive segun el flag persistido (puede ser false en
        // soft-delete) sin salir del contrato del record.
        return unidad.DefinirVigencia(entity.VigenteDesde, entity.VigenteHasta)
            with { IsActive = entity.IsActive };
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
            entity.TipoDocumento,
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

    private static void SetProperty<T>(T target, string propertyName, object? value)
        where T : EntidadBase
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró la propiedad '{propertyName}' en {typeof(T).Name}.");

        property.SetValue(target, value);
    }
}
