using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using SGV.Aplicacion.Personas.Comandos.Validaciones;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Personas.Comandos;

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
    /// <remarks>
    /// Issue #147 PR3: los validators de Persona
    /// (<see cref="CrearPersonaRequestValidator"/> y
    /// <see cref="ActualizarPersonaRequestValidator"/>) se registran
    /// explícitamente vía factory con
    /// <see cref="ITipoDocumentoCatalogoConsulta"/> inyectado. Esto es
    /// defensa en profundidad sobre
    /// <c>AddValidatorsFromAssemblyContaining</c> (que ya auto-wirea el
    /// ctor primario cuando el catálogo está registrado, pero deja el wiring
    /// implícito y propenso a una refactor accidental). Las tres reglas de
    /// catálogo (<c>FK_INEXISTENTE</c>, <c>LONGITUD_FUERA_DE_RANGO</c>,
    /// <c>PATRON_NO_CUMPLIDO</c>) requieren el catálogo en runtime; si
    /// alguien quita el factory, los validators vuelven a caer al ctor
    /// sin args y las reglas se cortocircuitan a <c>true</c>.
    /// </remarks>
    public static IServiceCollection AddAplicacionServicios(this IServiceCollection services)
    {
        // Register all FluentValidation validators from the Application layer assembly
        services.AddValidatorsFromAssemblyContaining<CrearUnidadOrganizativaRequestValidator>(ServiceLifetime.Scoped);

        // PR3 (issue #147): binding explícito de los validators de Persona
        // con el catálogo in-memory. El factory captura ITipoDocumentoCatalogoConsulta
        // del scope actual de DI para que las reglas async (FK_INEXISTENTE,
        // LONGITUD_FUERA_DE_RANGO, PATRON_NO_CUMPLIDO) ejecuten su consulta
        // contra el catálogo real.
        services.AddScoped<IValidator<CrearPersonaRequest>>(sp =>
            new CrearPersonaRequestValidator(sp.GetRequiredService<ITipoDocumentoCatalogoConsulta>()));
        services.AddScoped<IValidator<ActualizarPersonaRequest>>(sp =>
            new ActualizarPersonaRequestValidator(sp.GetRequiredService<ITipoDocumentoCatalogoConsulta>()));

        return services;
    }
}
