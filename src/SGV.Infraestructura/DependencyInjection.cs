using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Infraestructura.Persistencia.Repositorios;

namespace SGV.Infraestructura;

/// <summary>
/// Extension methods for registering infrastructure dependencies.
/// Works alongside existing DI registrations in the API project.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers repositories, query services, command services, and Unit of Work.
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
        services.AddScoped<ITipoUnidadOrganizativaRepository, TipoUnidadOrganizativaRepository>();
        services.AddScoped<INivelCargoRepository, NivelCargoRepository>();
        services.AddScoped<IPersonaRepository, PersonaRepository>();

        // Query services (application layer)
        services.AddScoped<IUnidadOrganizativaServicioConsulta, UnidadOrganizativaServicioConsulta>();
        services.AddScoped<ICargoServicioConsulta, CargoServicioConsulta>();
        services.AddScoped<IPuestoServicioConsulta, PuestoServicioConsulta>();
        services.AddScoped<IHabilidadServicioConsulta, HabilidadServicioConsulta>();
        services.AddScoped<ITipoUnidadOrganizativaServicioConsulta, TipoUnidadOrganizativaServicioConsulta>();
        services.AddScoped<INivelCargoServicioConsulta, NivelCargoServicioConsulta>();
        services.AddScoped<IPersonaServicioConsulta, PersonaServicioConsulta>();

        // Command services (application layer)
        services.AddScoped<IUnidadOrganizativaServicioComandos, UnidadOrganizativaServicioComandos>();
        services.AddScoped<ICargoServicioComandos, CargoServicioComandos>();
        services.AddScoped<IPuestoServicioComandos, PuestoServicioComandos>();
        services.AddScoped<IHabilidadServicioComandos, HabilidadServicioComandos>();
        services.AddScoped<IPersonaServicioComandos, PersonaServicioComandos>();

        return services;
    }
}
