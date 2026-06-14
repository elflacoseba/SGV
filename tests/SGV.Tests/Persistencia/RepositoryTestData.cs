using SGV.Dominio.Organizacion;

namespace SGV.Tests.Persistencia;

internal static class RepositoryTestData
{
    public static UnidadOrganizativa CreateUnidadOrganizativa(string prefix, bool isActive = true, bool isDeleted = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var unidad = new UnidadOrganizativa($"{prefix}-{suffix}", $"{prefix} {suffix}", "TEST");

        if (!isActive)
        {
            unidad.Desactivar();
        }

        if (isDeleted)
        {
            unidad.IsDeleted = true;
            unidad.DeletedAt = DateTime.UtcNow;
        }

        return unidad;
    }

    public static Cargo CreateCargo(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new Cargo($"{prefix}-{suffix}", $"{prefix} {suffix}", "TEST");
    }

    public static Puesto CreatePuesto(
        string prefix,
        UnidadOrganizativa unidadOrganizativa,
        Cargo cargo,
        bool isActive = true,
        bool isDeleted = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var puesto = new Puesto(unidadOrganizativa.Id, cargo.Id, $"{prefix}-{suffix}", $"{prefix} {suffix}");

        if (!isActive)
        {
            typeof(Puesto)
                .GetProperty(nameof(Puesto.IsActive))!
                .SetValue(puesto, false);
        }

        if (isDeleted)
        {
            puesto.IsDeleted = true;
            puesto.DeletedAt = DateTime.UtcNow;
        }

        return puesto;
    }
}
