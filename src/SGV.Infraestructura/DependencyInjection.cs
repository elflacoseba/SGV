using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Infraestructura.Email;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Infraestructura.Seguridad;

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
        // Constraint violation detector
        services.AddSingleton<IConstraintViolationDetector, MySqlConstraintViolationDetector>();

        // Unit of Work and explicit audit service for non-auditable Identity rows
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditoriaServicio, AuditoriaServicio>();

        // Repositories
        services.AddScoped<IUnidadOrganizativaRepository, UnidadOrganizativaRepository>();
        services.AddScoped<ICargoRepository, CargoRepository>();
        services.AddScoped<IPuestoRepository, PuestoRepository>();
        services.AddScoped<IHabilidadRepository, HabilidadRepository>();
        services.AddScoped<ICategoriaHabilidadRepository, CategoriaHabilidadRepository>();
        services.AddScoped<ITipoUnidadOrganizativaRepository, TipoUnidadOrganizativaRepository>();
        services.AddScoped<INivelCargoRepository, NivelCargoRepository>();
        services.AddScoped<ITipoDocumentoRepository, TipoDocumentoRepository>();
        services.AddScoped<IPersonaRepository, PersonaRepository>();
        services.AddScoped<ICargoSkillRepository, CargoSkillRepository>();
        services.AddScoped<IPersonaSkillRepository, PersonaSkillRepository>();
        services.AddScoped<INivelHabilidadRepository, NivelHabilidadRepository>();
        services.AddScoped<IOcupacionRepository, OcupacionRepository>();
        services.AddScoped<ISkillCargoRepository, SkillCargoRepository>();
        services.AddScoped<ISkillPersonaRepository, SkillPersonaRepository>();

        // Query services (application layer)
        services.AddScoped<IUnidadOrganizativaServicioConsulta, UnidadOrganizativaServicioConsulta>();
        services.AddScoped<ICargoServicioConsulta, CargoServicioConsulta>();
        services.AddScoped<IPuestoServicioConsulta, PuestoServicioConsulta>();
        services.AddScoped<IHabilidadServicioConsulta, HabilidadServicioConsulta>();
        services.AddScoped<ICategoriaHabilidadServicioConsulta, CategoriaHabilidadServicioConsulta>();
        services.AddScoped<ITipoUnidadOrganizativaServicioConsulta, TipoUnidadOrganizativaServicioConsulta>();
        services.AddScoped<INivelCargoServicioConsulta, NivelCargoServicioConsulta>();
        services.AddScoped<ITipoDocumentoCatalogoConsulta, TipoDocumentoCatalogoConsulta>();
        services.AddScoped<INivelHabilidadServicioConsulta, NivelHabilidadServicioConsulta>();
        services.AddScoped<IPersonaServicioConsulta, PersonaServicioConsulta>();
        services.AddScoped<IOcupacionServicioConsulta, OcupacionServicioConsulta>();
        services.AddScoped<ISkillCargoServicioConsulta, SkillCargoServicioConsulta>();
        services.AddScoped<ISkillPersonaServicioConsulta, SkillPersonaServicioConsulta>();

        // Command services (application layer)
        services.AddScoped<IUnidadOrganizativaServicioComandos, UnidadOrganizativaServicioComandos>();
        services.AddScoped<ICargoServicioComandos, CargoServicioComandos>();
        services.AddScoped<IPuestoServicioComandos, PuestoServicioComandos>();
        services.AddScoped<IHabilidadServicioComandos, HabilidadServicioComandos>();
        services.AddScoped<IPersonaServicioComandos, PersonaServicioComandos>();
        services.AddScoped<IOcupacionServicioComandos, OcupacionServicioComandos>();

        // Skill assignment services (application layer)
        services.AddScoped<ICargoSkillServicio, CargoSkillServicio>();
        services.AddScoped<IPersonaSkillServicio, PersonaSkillServicio>();

        // Identity user/role services
        services.AddScoped<IUsuarioServicioComandos, UsuarioServicioComandos>();
        services.AddScoped<IRolServicioConsulta, RolServicioConsulta>();
        services.AddScoped<UsuarioIdentityGateway>();
        services.AddScoped<IUsuarioIdentityGateway>(sp => sp.GetRequiredService<UsuarioIdentityGateway>());
        services.AddScoped<IUsuarioServicioConsulta>(sp => sp.GetRequiredService<UsuarioIdentityGateway>());
        services.AddScoped<IAuthServicio, AuthServicio>();

        // Identity IEmailSender — backs the password reset flow. The
        // sender is registered as Singleton so the underlying MailKit
        // client lifetime is shared across requests. Logger mode in
        // Development means no real SMTP connection is opened.
        services.AddSingleton<IEmailSender<SgvIdentityUser>, SmtpEmailSender>();

        // Password reset (issue #181): scoped because the service holds
        // scoped dependencies (UserManager<SgvIdentityUser>); a singleton
        // here would capture a stale UserManager across requests.
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        return services;
    }
}
