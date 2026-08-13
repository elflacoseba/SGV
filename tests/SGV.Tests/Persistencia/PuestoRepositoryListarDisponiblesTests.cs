using Microsoft.EntityFrameworkCore;
using SGV.Dominio.Ocupaciones;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests <c>[MySqlFact]</c> para <see cref="PuestoRepository.ListarDisponiblesAsync"/>
/// (REQ-PTO-DISP-001). Cubren los dos <c>NOT EXISTS</c> sobre <c>Ocupaciones</c>
/// y <c>Vacantes</c>, la exclusion de puestos inactivos / soft-deleted, el
/// boundary de <c>EsVigente</c> / <c>EsAbierta</c> por FechaFin / FechaCierre
/// no nulos y el orden estable <c>Nombre ASC, Codigo ASC</c>. Estructura:
/// un <c>[Theory]+[InlineData]</c> para la matrix Ocupación-vigente ×
/// Vacante-abierta (4 cuadrantes) más métodos <c>[MySqlFact]</c> discretos
/// para los ejes ortogonales (soft-delete, boundary, orden). <c>try/finally</c>
/// con cleanup topológico en cada escenario. Se skipean limpio sin MySQL
/// disponible (configuración estándar del repo).
/// </summary>
public sealed class PuestoRepositoryListarDisponiblesTests
{
    [MySqlFact]
    public async Task ListarDisponibles_MySql_InactivoOSoftDeleted_ExcluyeAmbos()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"PT-DISP-INA-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"PT-DISP-INA-CARGO-{suffix}");
        var inactivo = RepositoryTestData.CreatePuesto($"PT-DISP-INA-INACT-{suffix}", unidad, cargo,
            isActive: false, isDeleted: false);
        var softDeleted = RepositoryTestData.CreatePuesto($"PT-DISP-INA-DEL-{suffix}", unidad, cargo,
            isActive: true, isDeleted: true);
        softDeleted.DeletedAt = DateTime.UtcNow;
        var disponible = RepositoryTestData.CreatePuesto($"PT-DISP-INA-OK-{suffix}", unidad, cargo);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync([inactivo, softDeleted, disponible]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var todos = await repo.ListarDisponiblesAsync(default);

            var subset = todos
                .Where(p => p.Id == inactivo.Id || p.Id == softDeleted.Id || p.Id == disponible.Id)
                .ToArray();

            // Solo el Puesto control (sin flags) debe sobrevivir.
            Assert.Single(subset);
            Assert.Equal(disponible.Id, subset[0].Id);
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(inactivo, softDeleted, disponible);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Cuadrantes estrictos del filtro de disponibilidad sobre el Puesto
    /// bajo prueba — parametrización de la combinación binaria
    /// (con Ocupación vigente × con Vacante abierta). Cubre los cuatro
    /// cuadrantes: ni vigente ni abierta → incluido; vigente → excluido;
    /// abierta → excluido; ambas → excluido (cualquiera basta). La
    /// parametrización colapsa lo que antes eran tres MySqlFact discretos
    /// (<c>ConOcupacionVigente_Excluye</c>, <c>ConVacanteAbierta_Excluye</c>,
    /// <c>CasoCombinadoOcupacionYVacante_ExcluidoPorOcupacion</c>) sin
    /// reducir la cobertura de la matrix. Los escenarios ortogonales
    /// (soft-delete, boundary de <c>EsVigente</c>/<c>EsAbierta</c> por
    /// FechaFin/FechaCierre no nulos, orden estable) quedan cubiertos
    /// por separado más abajo.
    /// </summary>
    [Theory]
    [InlineData(false, false, true)]   // sin Ocupación vigente + sin Vacante abierta → incluido
    [InlineData(true, false, false)]   // Ocupación vigente + sin Vacante abierta   → excluido (N1)
    [InlineData(false, true, false)]   // sin Ocupación vigente + Vacante abierta   → excluido
    [InlineData(true, true, false)]    // ambas vigentes/abiertas                    → excluido
    public async Task ListarDisponibles_MySql_MatrixOcupacionYVacante_ClasificaCorrectamente(
        bool conOcupacionVigente,
        bool conVacanteAbierta,
        bool puestoDebeEstarIncluido)
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"PT-DISP-MX-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"PT-DISP-MX-CARGO-{suffix}");
        var persona = RepositoryTestData.CreatePersona($"PT-DISP-MX-PER-{suffix}");
        var estadoAbierta = CrearEstadoVacante($"PT-DISP-MX-EST-{suffix}", "Abierta", esTerminal: false);
        var puesto = RepositoryTestData.CreatePuesto($"PT-DISP-MX-PTO-{suffix}", unidad, cargo);

        OcupacionEntity? ocupacion = null;
        VacanteEntity? vacante = null;

        var seed = new List<object> { unidad, cargo, puesto };
        if (conOcupacionVigente)
        {
            seed.Add(persona);
            ocupacion = CrearOcupacion(persona.Id, puesto.Id, $"PT-DISP-MX-{suffix}", fechaFin: null);
            seed.Add(ocupacion);
        }
        if (conVacanteAbierta)
        {
            seed.Add(estadoAbierta);
            vacante = CrearVacante(puesto.Id, estadoAbierta.Id, $"PT-DISP-MX-{suffix}", fechaCierre: null);
            seed.Add(vacante);
        }
        await SeedAsync(context, seed.ToArray());

        try
        {
            var repo = new PuestoRepository(context);
            var todos = await repo.ListarDisponiblesAsync(default);

            if (puestoDebeEstarIncluido)
            {
                Assert.Contains(todos, p => p.Id == puesto.Id);
            }
            else
            {
                Assert.DoesNotContain(todos, p => p.Id == puesto.Id);
            }
        }
        finally
        {
            // Cleanup topológico: Vacante → Ocupación → Puesto → Cargo → UO → ...
            // Si no incluimos explícitamente la Ocupación/Vacante seedeada, EF
            // lanza "association severed" al intentar remover el Puesto.
            var cleanup = new List<object> { cargo, unidad, puesto };
            if (conOcupacionVigente)
            {
                cleanup.Add(ocupacion!);
                cleanup.Add(persona);
            }
            if (conVacanteAbierta)
            {
                cleanup.Add(vacante!);
                cleanup.Add(estadoAbierta);
            }
            var ordered = cleanup.OrderBy(e => e switch
            {
                VacanteEntity => 0,
                OcupacionEntity => 1,
                CargoEntity => 3,
                UnidadOrganizativaEntity => 4,
                EstadoVacanteEntity => 5,
                PersonaEntity => 6,
                _ => 99
            });
            await CleanupAsync(context, ordered.ToArray());
        }
    }

    [MySqlFact]
    public async Task ListarDisponibles_MySql_OcupacionFinalizada_NoExcluye()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"PT-DISP-OCF-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"PT-DISP-OCF-CARGO-{suffix}");
        var persona = RepositoryTestData.CreatePersona($"PT-DISP-OCF-PER-{suffix}");
        var puesto = RepositoryTestData.CreatePuesto($"PT-DISP-OCF-FIN-{suffix}", unidad, cargo);
        var ocupacion = CrearOcupacion(persona.Id, puesto.Id, $"PT-DISP-OCF-{suffix}",
            fechaFin: new DateOnly(2024, 6, 30));

        await SeedAsync(context, unidad, cargo, persona, puesto, ocupacion);

        try
        {
            var repo = new PuestoRepository(context);
            var todos = await repo.ListarDisponiblesAsync(default);

            // Ocupacion finalizada libera al Puesto: debe quedar disponible.
            Assert.Contains(todos, p => p.Id == puesto.Id);
        }
        finally
        {
            await CleanupAsync(context, unidad, cargo, persona, puesto, ocupacion);
        }
    }

    [MySqlFact]
    public async Task ListarDisponibles_MySql_VacanteCubierta_NoExcluye()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"PT-DISP-VCB-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"PT-DISP-VCB-CARGO-{suffix}");
        var estadoCubierta = CrearEstadoVacante($"PT-DISP-VCB-EST-{suffix}", "Cubierta", esTerminal: true);
        var puesto = RepositoryTestData.CreatePuesto($"PT-DISP-VCB-CUB-{suffix}", unidad, cargo);
        var vacante = CrearVacante(puesto.Id, estadoCubierta.Id, $"PT-DISP-VCB-{suffix}",
            fechaCierre: new DateTime(2024, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        await SeedAsync(context, unidad, cargo, estadoCubierta, puesto, vacante);

        try
        {
            var repo = new PuestoRepository(context);
            var todos = await repo.ListarDisponiblesAsync(default);

            // Vacante Cubierta (FechaCierre != null) no bloquea al Puesto.
            Assert.Contains(todos, p => p.Id == puesto.Id);
        }
        finally
        {
            await CleanupAsync(context, vacante, puesto, cargo, unidad, estadoCubierta);
        }
    }

    [MySqlFact]
    public async Task ListarDisponibles_MySql_SoloDisponibles_OrdenadosPorNombreYCodigo()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"PT-DISP-ORD-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"PT-DISP-ORD-CARGO-{suffix}");

        // Tres Puestos disponibles con nombres en orden intencionalmente
        // mezclado para forzar el orden por Nombre + Codigo.
        var primero = RepositoryTestData.CreatePuesto($"PT-DISP-ORD-PRI-{suffix}", unidad, cargo);
        primero.Nombre = "Alpha";
        var segundo = RepositoryTestData.CreatePuesto($"PT-DISP-ORD-SEG-{suffix}", unidad, cargo);
        segundo.Nombre = "Alpha";
        var tercero = RepositoryTestData.CreatePuesto($"PT-DISP-ORD-TER-{suffix}", unidad, cargo);
        tercero.Nombre = "Beta";

        // Puesto con Ocupacion vigente (no debe aparecer).
        var persona = RepositoryTestData.CreatePersona($"PT-DISP-ORD-PER-{suffix}");
        var ocupado = RepositoryTestData.CreatePuesto($"PT-DISP-ORD-OCU-{suffix}", unidad, cargo);
        var ocupacion = CrearOcupacion(persona.Id, ocupado.Id, $"PT-DISP-ORD-{suffix}", fechaFin: null);

        await SeedAsync(context, unidad, cargo, persona, primero, segundo, tercero, ocupado, ocupacion);

        try
        {
            var repo = new PuestoRepository(context);
            var todos = await repo.ListarDisponiblesAsync(default);

            var subset = todos
                .Where(p => p.Id == primero.Id || p.Id == segundo.Id || p.Id == tercero.Id)
                .ToArray();

            Assert.Equal(3, subset.Length);
            // Orden esperado: "Alpha" + Codigo ASC (PRI, SEG) y luego "Beta".
            Assert.Equal(primero.Id, subset[0].Id);
            Assert.Equal(segundo.Id, subset[1].Id);
            Assert.Equal(tercero.Id, subset[2].Id);
            Assert.DoesNotContain(todos, p => p.Id == ocupado.Id);
        }
        finally
        {
            await CleanupAsync(context, ocupacion, ocupado, primero, segundo, tercero, cargo, unidad, persona);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8];

    private static OcupacionEntity CrearOcupacion(
        Guid personaId, Guid puestoId, string prefix, DateOnly? fechaFin, bool isDeleted = false)
    {
        return new OcupacionEntity
        {
            Id = Guid.NewGuid(),
            PersonaId = personaId,
            PuestoId = puestoId,
            FechaInicio = new DateOnly(2024, 1, 15),
            FechaFin = fechaFin,
            TipoAsignacion = TipoAsignacion.Permanente,
            Observaciones = prefix,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static EstadoVacanteEntity CrearEstadoVacante(string prefix, string nombre, bool esTerminal)
    {
        return new EstadoVacanteEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"{prefix}-COD",
            Nombre = nombre,
            Orden = 1,
            EsTerminal = esTerminal
        };
    }

    private static VacanteEntity CrearVacante(
        Guid puestoId, Guid estadoVacanteId, string prefix, DateTime? fechaCierre)
    {
        return new VacanteEntity
        {
            Id = Guid.NewGuid(),
            PuestoId = puestoId,
            EstadoVacanteId = estadoVacanteId,
            Motivo = $"{prefix}-MOTIVO",
            FechaApertura = DateTime.UtcNow,
            FechaCierre = fechaCierre,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static async Task SeedAsync(SgvDbContext context, params object[] entities)
    {
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case UnidadOrganizativaEntity u:
                    await context.Set<UnidadOrganizativaEntity>().AddAsync(u);
                    break;
                case CargoEntity c:
                    await context.Set<CargoEntity>().AddAsync(c);
                    break;
                case PersonaEntity p:
                    await context.Set<PersonaEntity>().AddAsync(p);
                    break;
                case PuestoEntity p:
                    await context.Set<PuestoEntity>().AddAsync(p);
                    break;
                case OcupacionEntity o:
                    await context.Set<OcupacionEntity>().AddAsync(o);
                    break;
                case EstadoVacanteEntity e:
                    await context.Set<EstadoVacanteEntity>().AddAsync(e);
                    break;
                case VacanteEntity v:
                    await context.Set<VacanteEntity>().AddAsync(v);
                    break;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task CleanupAsync(SgvDbContext context, params object[] entities)
    {
        // Orden topologico: dependientes primero, principales al final.
        // Sin esto, EF lanza "association severed" al intentar remover un
        // Puesto mientras su Vacante (FK RESTRICT) o su Ocupacion
        // (FK RESTRICT) siguen trackeadas.
        var ordered = entities.OrderBy(e => e switch
        {
            VacanteEntity => 0,
            OcupacionEntity => 1,
            PuestoEntity => 2,
            CargoEntity => 3,
            UnidadOrganizativaEntity => 4,
            EstadoVacanteEntity => 5,
            PersonaEntity => 6,
            _ => 99,
        });

        foreach (var entity in ordered)
        {
            switch (entity)
            {
                case VacanteEntity v:
                    context.Set<VacanteEntity>().Remove(v);
                    break;
                case OcupacionEntity o:
                    context.Set<OcupacionEntity>().Remove(o);
                    break;
                case PuestoEntity p:
                    context.Set<PuestoEntity>().Remove(p);
                    break;
                case CargoEntity c:
                    context.Set<CargoEntity>().Remove(c);
                    break;
                case UnidadOrganizativaEntity u:
                    context.Set<UnidadOrganizativaEntity>().Remove(u);
                    break;
                case EstadoVacanteEntity e:
                    context.Set<EstadoVacanteEntity>().Remove(e);
                    break;
                case PersonaEntity p:
                    context.Set<PersonaEntity>().Remove(p);
                    break;
            }
        }

        await context.SaveChangesAsync();
    }
}
