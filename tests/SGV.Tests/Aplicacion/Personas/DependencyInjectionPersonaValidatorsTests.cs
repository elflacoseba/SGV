using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion;
using SGV.Aplicacion.Personas.Comandos.Validaciones;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Aplicacion.Personas;

/// <summary>
/// Tests de wiring del DI del módulo de Aplicación (issue #147 PR3):
/// garantizan que los validators de <see cref="CrearPersonaRequest"/> y
/// <see cref="ActualizarPersonaRequest"/> se registran con el catálogo
/// inyectado vía <see cref="DependencyInjection.AddAplicacionServicios"/>,
/// no con el ctor sin args. Esto cierra el gap de PR2 donde
/// <c>AddValidatorsFromAssemblyContaining</c> usaba el ctor sin args y
/// las reglas de catálogo se cortocircuitaban a <c>true</c> en runtime.
/// <para>
/// El test ejercita el path de DI real (no el de los tests unitarios)
/// para que un cambio accidental que vuelva a registrar los validators
/// sin catálogo se detecte en CI.
/// </para>
/// </summary>
public sealed class DependencyInjectionPersonaValidatorsTests
{
    /// <summary>
    /// Stub en memoria de <see cref="ITipoDocumentoRepository"/> que devuelve
    /// sólo las filas seed de <see cref="TipoDocumentoConstantes"/>. Vive en
    /// este archivo de tests para no contaminar producción con un
    /// repository-side fake.
    /// </summary>
    private sealed class StubTipoDocumentoRepository : ITipoDocumentoRepository
    {
        private static TipoDocumento Build(string codigo, string nombre, string patron, int min, int max)
        {
            // El id viene de la constante, no del Guid.NewGuid() default.
            // Usamos `with { Id = ... }` (record inheritance) para setear el
            // identificador en el seed in-memory sin exponer un ctor extra.
            var tipo = new TipoDocumento(codigo, nombre, patron, min, max);
            return tipo with { Id = codigo switch
            {
                TipoDocumentoConstantes.DniCodigo => TipoDocumentoConstantes.DniId,
                TipoDocumentoConstantes.LeCodigo => TipoDocumentoConstantes.LeId,
                TipoDocumentoConstantes.LcCodigo => TipoDocumentoConstantes.LcId,
                TipoDocumentoConstantes.PasaporteCodigo => TipoDocumentoConstantes.PasaporteId,
                _ => Guid.Empty
            } };
        }

        private static readonly IReadOnlyList<TipoDocumento> Seed = new[]
        {
            Build(TipoDocumentoConstantes.DniCodigo, TipoDocumentoConstantes.DniNombre,
                TipoDocumentoConstantes.DniPatron, TipoDocumentoConstantes.DniLongitudMinima, TipoDocumentoConstantes.DniLongitudMaxima),
            Build(TipoDocumentoConstantes.LeCodigo, TipoDocumentoConstantes.LeNombre,
                TipoDocumentoConstantes.LePatron, TipoDocumentoConstantes.LeLongitudMinima, TipoDocumentoConstantes.LeLongitudMaxima),
            Build(TipoDocumentoConstantes.LcCodigo, TipoDocumentoConstantes.LcNombre,
                TipoDocumentoConstantes.LcPatron, TipoDocumentoConstantes.LcLongitudMinima, TipoDocumentoConstantes.LcLongitudMaxima),
            Build(TipoDocumentoConstantes.PasaporteCodigo, TipoDocumentoConstantes.PasaporteNombre,
                TipoDocumentoConstantes.PasaportePatron, TipoDocumentoConstantes.PasaporteLongitudMinima, TipoDocumentoConstantes.PasaporteLongitudMaxima)
        };

        public Task<IReadOnlyList<TipoDocumento>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Seed);

        public Task<TipoDocumento?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Seed.FirstOrDefault(t => t.Id == id));

        public Task<TipoDocumento?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
            => Task.FromResult(Seed.FirstOrDefault(t => t.Codigo == codigo));
    }

    private static ServiceProvider BuildServiceProviderWithCatalogo()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // ITipoDocumentoRepository necesita SgvDbContext en el ctor primario.
        // Para este test no necesitamos tocar MySQL: el repo real sólo se usa
        // si la consulta entra al catálogo, y los tests Assert de este archivo
        // cubren los 4 escenarios del seed + el Guid inválido. Como sólo
        // ejercitamos la rama in-memory vía el stub, nos ahorramos el DbContext.
        services.AddScoped<ITipoDocumentoRepository>(_ => new StubTipoDocumentoRepository());
        services.AddScoped<ITipoDocumentoCatalogoConsulta, TipoDocumentoCatalogoConsulta>();

        // Punto crítico: el wiring bajo prueba.
        services.AddAplicacionServicios();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task Resolved_CrearValidator_WithTipoDocumentoEnCatalogo_PeroNumeroInvalido_DebeRechazarPorPatron()
    {
        // AC A2 (issue #147 PR3): cuando el request trae TipoDocumentoId
        // válido (DNI) pero NumeroDocumento "12A45678" (no matchea el
        // patrón `^\d{7,8}$`), el validator resolved desde DI real debe
        // rechazar. Antes de PR3, AddValidatorsFromAssemblyContaining usaba
        // el ctor sin args, que cortocircuitaba el catálogo a true y el
        // IsValid=true fallaba silenciosamente.
        using var sp = BuildServiceProviderWithCatalogo();
        using var scope = sp.CreateScope();

        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CrearPersonaRequest>>();

        var request = new CrearPersonaRequest(
            Legajo: "LEG-001",
            Nombres: "Juan",
            Apellidos: "Pérez",
            Email: null,
            TipoDocumentoId: TipoDocumentoConstantes.DniId,
            NumeroDocumento: "12A45678",
            Telefono: null);

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CrearPersonaRequest.NumeroDocumento));
    }

    [Fact]
    public async Task Resolved_CrearValidator_WithTipoDocumentoValido_YNumeroValido_DebeSerValido()
    {
        // AC: con TipoDocumentoId=DniId y NumeroDocumento="12345678", las
        // reglas de catálogo (patrón + longitud) se ejecutan y el request
        // pasa. Esto confirma que el wiring no introduce falsos positivos.
        using var sp = BuildServiceProviderWithCatalogo();
        using var scope = sp.CreateScope();

        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CrearPersonaRequest>>();

        var request = new CrearPersonaRequest(
            Legajo: "LEG-001",
            Nombres: "Juan",
            Apellidos: "Pérez",
            Email: null,
            TipoDocumentoId: TipoDocumentoConstantes.DniId,
            NumeroDocumento: "12345678",
            Telefono: null);

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Resolved_CrearValidator_WithTipoDocumentoIdFueraDeCatalogo_DebeRechazarPorFK()
    {
        // AC: con un Guid que no está en el catálogo seed, el validator
        // emite FK_INEXISTENTE. Sin DI con catálogo, el validator con ctor
        // sin args devolvería IsValid=true (porque cortocircuita el catálogo).
        using var sp = BuildServiceProviderWithCatalogo();
        using var scope = sp.CreateScope();

        var validator = scope.ServiceProvider.GetRequiredService<IValidator<CrearPersonaRequest>>();

        var idFueraDeCatalogo = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var request = new CrearPersonaRequest(
            Legajo: "LEG-001",
            Nombres: "Juan",
            Apellidos: "Pérez",
            Email: null,
            TipoDocumentoId: idFueraDeCatalogo,
            NumeroDocumento: "12345678",
            Telefono: null);

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorCode == "FK_INEXISTENTE");
    }

    [Fact]
    public async Task Resolved_ActualizarValidator_DebeEstarRegistradoConCatalogoTambien()
    {
        // AC: el validator de actualización usa el mismo wiring. Esta
        // aserción cubre el caso simétrico que el spec requiere en REQ-PM-04
        // ("Actualizar documento con patrón inválido").
        using var sp = BuildServiceProviderWithCatalogo();
        using var scope = sp.CreateScope();

        var validator = scope.ServiceProvider.GetRequiredService<IValidator<ActualizarPersonaRequest>>();

        var request = new ActualizarPersonaRequest(
            Legajo: "LEG-001",
            Nombres: "Juan",
            Apellidos: "Pérez",
            Email: null,
            TipoDocumentoId: TipoDocumentoConstantes.PasaporteId,
            NumeroDocumento: "12345",
            Telefono: null);

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ActualizarPersonaRequest.NumeroDocumento));
    }
}
