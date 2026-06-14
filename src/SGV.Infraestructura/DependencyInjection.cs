using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Infraestructura.Persistencia.Repositorios;

namespace SGV.Infraestructura;

/// <summary>
/// Extension methods for registering infrastructure dependencies.
/// Works alongside existing DI registrations in the API project.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers read-only repositories, query services, and Unit of Work.
    /// </summary>
    public static IServiceCollection AddInfraestructuraServicios(this IServiceCollection services)
    {
        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<IUnidadOrganizativaRepository, UnidadOrganizativaRepository>();
        services.AddScoped<ICargoRepository, CargoRepository>();
        services.AddScoped<IPuestoRepository, PuestoRepository>();
        services.AddScoped<IHabilidadRepository, HabilidadRepository>();

        // Query services (application layer)
        services.AddScoped<IUnidadOrganizativaServicioConsulta, UnidadOrganizativaServicioConsulta>();
        services.AddScoped<ICargoServicioConsulta, CargoServicioConsulta>();
        services.AddScoped<IPuestoServicioConsulta, PuestoServicioConsulta>();
        services.AddScoped<IHabilidadServicioConsulta, HabilidadServicioConsulta>();

        return services;
    }
}
