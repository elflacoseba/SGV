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
        var cargo = new Cargo(entity.Codigo, entity.Nombre, entity.NivelId, entity.Descripcion)
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

        SetProperty(cargo, nameof(Cargo.IsActive), entity.IsActive);

        if (entity.NivelCargo is not null)
        {
            SetProperty(cargo, nameof(Cargo.NivelCargo), ToDomain(entity.NivelCargo));
        }

        return cargo;
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
        var puesto = new Puesto(entity.UnidadOrganizativaId, entity.CargoId, entity.Codigo, entity.Nombre, entity.PuestoSuperiorId)
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

        puesto.CambiarDatos(entity.Codigo, entity.Nombre, entity.Descripcion);
        SetProperty(puesto, nameof(Puesto.IsActive), entity.IsActive);

        if (entity.UnidadOrganizativa is not null)
        {
            SetProperty(puesto, nameof(Puesto.UnidadOrganizativa), ToDomain(entity.UnidadOrganizativa));
        }

        if (entity.Cargo is not null)
        {
            SetProperty(puesto, nameof(Puesto.Cargo), ToDomain(entity.Cargo));
        }

        return puesto;
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
        var persona = new Persona(entity.Nombres, entity.Apellidos, entity.Legajo, entity.Email)
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

        SetProperty(persona, nameof(Persona.IsActive), entity.IsActive);
        SetProperty(persona, nameof(Persona.Telefono), entity.Telefono);
        SetProperty(persona, nameof(Persona.TipoDocumento), entity.TipoDocumento);
        SetProperty(persona, nameof(Persona.NumeroDocumento), entity.NumeroDocumento);

        return persona;
    }

    public static Ocupacion ToDomain(OcupacionEntity entity)
    {
        var ocupacion = new Ocupacion(entity.PersonaId, entity.PuestoId, entity.FechaInicio, entity.TipoAsignacion, entity.FechaFin, entity.Observaciones)
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

        if (entity.Persona is not null)
        {
            SetProperty(ocupacion, nameof(Ocupacion.Persona), ToDomain(entity.Persona));
        }

        if (entity.Puesto is not null)
        {
            SetProperty(ocupacion, nameof(Ocupacion.Puesto), ToDomain(entity.Puesto));
        }

        return ocupacion;
    }

    private static void SetProperty<T>(T target, string propertyName, object? value)
        where T : EntidadBase
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró la propiedad '{propertyName}' en {typeof(T).Name}.");

        property.SetValue(target, value);
    }
}
