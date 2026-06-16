using SGV.Infraestructura.Persistencia.Catalogos;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Crea datos de prueba para tests de repositorio usando tipos *Entity.
/// Los mapeos a tipos de Dominio se realizan en los repositorios (PR 2).
/// </summary>
internal static class RepositoryTestData
{
    public static UnidadOrganizativaEntity CreateUnidadOrganizativa(string prefix, bool isActive = true, bool isDeleted = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var unidad = new UnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"{prefix}-{suffix}",
            Nombre = $"{prefix} {suffix}",
            TipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId,
            IsActive = isActive,
            IsDeleted = isDeleted
        };

        return unidad;
    }

    public static CargoEntity CreateCargo(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new CargoEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"{prefix}-{suffix}",
            Nombre = $"{prefix} {suffix}",
            Nivel = "TEST",
            IsActive = true
        };
    }

    public static PuestoEntity CreatePuesto(
        string prefix,
        UnidadOrganizativaEntity unidadOrganizativa,
        CargoEntity cargo,
        bool isActive = true,
        bool isDeleted = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new PuestoEntity
        {
            Id = Guid.NewGuid(),
            UnidadOrganizativaId = unidadOrganizativa.Id,
            CargoId = cargo.Id,
            Codigo = $"{prefix}-{suffix}",
            Nombre = $"{prefix} {suffix}",
            IsActive = isActive,
            IsDeleted = isDeleted
        };
    }
}
