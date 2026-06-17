using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;

namespace SGV.Aplicacion;

/// <summary>
/// Extension methods for registering application-layer services,
/// including FluentValidation validators.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers application services and FluentValidation validators from the
    /// SGV.Aplicacion assembly.
    /// </summary>
    public static IServiceCollection AddAplicacionServicios(this IServiceCollection services)
    {
        // Register all FluentValidation validators from the Application layer assembly
        services.AddValidatorsFromAssemblyContaining<CrearUnidadOrganizativaRequestValidator>(ServiceLifetime.Scoped);

        return services;
    }
}
