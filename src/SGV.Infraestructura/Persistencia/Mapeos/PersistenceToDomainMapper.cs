using System.Reflection;
using SGV.Dominio.Comun;
using SGV.Dominio.Habilidades;
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
        var habilidad = new Habilidad(entity.Codigo, entity.Nombre, entity.Categoria, entity.Descripcion)
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

        SetProperty(habilidad, nameof(Habilidad.IsActive), entity.IsActive);
        return habilidad;
    }

    public static UnidadOrganizativa ToDomain(UnidadOrganizativaEntity entity)
    {
        var unidad = new UnidadOrganizativa(entity.Codigo, entity.Nombre, entity.TipoUnidadOrganizativaId, entity.UnidadPadreId)
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

        unidad.CambiarDatos(entity.Codigo, entity.Nombre, entity.TipoUnidadOrganizativaId, entity.Descripcion);
        unidad.DefinirVigencia(entity.VigenteDesde, entity.VigenteHasta);
        SetProperty(unidad, nameof(UnidadOrganizativa.IsActive), entity.IsActive);

        if (entity.UnidadPadre is not null)
        {
            SetProperty(unidad, nameof(UnidadOrganizativa.UnidadPadre), ToDomain(entity.UnidadPadre));
        }

        if (entity.TipoUnidadOrganizativa is not null)
        {
            SetProperty(unidad, nameof(UnidadOrganizativa.TipoUnidadOrganizativa), ToDomain(entity.TipoUnidadOrganizativa));
        }

        return unidad;
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

    private static void SetProperty<T>(T target, string propertyName, object? value)
        where T : EntidadBase
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró la propiedad '{propertyName}' en {typeof(T).Name}.");

        property.SetValue(target, value);
    }
}
