using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Mapeos;

/// <summary>
/// Maps domain <see cref="UnidadOrganizativa"/> instances to persistence entities for write operations.
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
}
