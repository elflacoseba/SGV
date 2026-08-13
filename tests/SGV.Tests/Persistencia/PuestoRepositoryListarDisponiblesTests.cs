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
/// y <c>Vacantes</c>, la exclusion de puestos inactivos / soft-deleted y el
/// orden estable <c>Nombre ASC, Codigo ASC</c>. Espejo de
/// <c>PuestoRepositoryQueryAsyncTests</c> en estructura (1 metodo por
/// escenario, <c>try/finally</c> con cleanup topologico). Se skipean limpio
/// sin MySQL disponible (configuracion estandar del repo).
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

    [MySqlFact]
    public async Task ListarDisponibles_MySql_ConOcupacionVigente_Excluye()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"PT-DISP-OCV-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"PT-DISP-OCV-CARGO-{suffix}");
        var persona = RepositoryTestData.CreatePersona($"PT-DISP-OCV-PER-{suffix}");
        var ocupado = RepositoryTestData.CreatePuesto($"PT-DISP-OCV-OCU-{suffix}", unidad, cargo);
        var ocupacion = CrearOcupacion(persona.Id, ocupado.Id, $"PT-DISP-OCV-{suffix}", fechaFin: null);
        var libre = RepositoryTestData.CreatePuesto($"PT-DISP-OCV-LIB-{suffix}", unidad, cargo);

        await SeedAsync(context, unidad, cargo, persona, ocupado, libre, ocupacion);

        try
        {
            var repo = new PuestoRepository(context);
            var todos = await repo.ListarDisponiblesAsync(default);

            var subset = todos
                .Where(p => p.Id == ocupado.Id || p.Id == libre.Id)
                .ToArray();

            Assert.Single(subset);
            Assert.Equal(libre.Id, subset[0].Id);
            Assert.DoesNotContain(todos, p => p.Id == ocupado.Id);
        }
        finally
        {
            await CleanupAsync(context, unidad, cargo, persona, ocupado, libre, ocupacion);
        }
    }

    [MySqlFact]
    public async Task ListarDisponibles_MySql_ConVacanteAbierta_Excluye()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"PT-DISP-VAB-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"PT-DISP-VAB-CARGO-{suffix}");
        var estadoAbierta = CrearEstadoVacante($"PT-DISP-VAB-EST-{suffix}", "Abierta", esTerminal: false);
        var conVacante = RepositoryTestData.CreatePuesto($"PT-DISP-VAB-VAC-{suffix}", unidad, cargo);
        var vacante = CrearVacante(conVacante.Id, estadoAbierta.Id, $"PT-DISP-VAB-{suffix}", fechaCierre: null);
        var libre = RepositoryTestData.CreatePuesto($"PT-DISP-VAB-LIB-{suffix}", unidad, cargo);

        await SeedAsync(context, unidad, cargo, estadoAbierta, conVacante, libre, vacante);

        try
        {
            var repo = new PuestoRepository(context);
            var todos = await repo.ListarDisponiblesAsync(default);

            var subset = todos
                .Where(p => p.Id == conVacante.Id || p.Id == libre.Id)
                .ToArray();

            Assert.Single(subset);
            Assert.Equal(libre.Id, subset[0].Id);
            Assert.DoesNotContain(todos, p => p.Id == conVacante.Id);
        }
        finally
        {
            await CleanupAsync(context, unidad, cargo, estadoAbierta, conVacante, libre, vacante);
        }
    }

    [MySqlFact]
    public async Task ListarDisponibles_MySql_CasoCombinadoOcupacionYVacante_ExcluidoPorOcupacion()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var suffix = UniqueSuffix();
        var unidad = RepositoryTestData.CreateUnidadOrganizativa($"PT-DISP-CTB-UO-{suffix}");
        var cargo = RepositoryTestData.CreateCargo($"PT-DISP-CTB-CARGO-{suffix}");
        var persona = RepositoryTestData.CreatePersona($"PT-DISP-CTB-PER-{suffix}");
        var estadoAbierta = CrearEstadoVacante($"PT-DISP-CTB-EST-{suffix}", "Abierta", esTerminal: false);
        var combinado = RepositoryTestData.CreatePuesto($"PT-DISP-CTB-COMBO-{suffix}", unidad, cargo);
        var ocupacion = CrearOcupacion(persona.Id, combinado.Id, $"PT-DISP-CTB-{suffix}", fechaFin: null);
        var vacante = CrearVacante(combinado.Id, estadoAbierta.Id, $"PT-DISP-CTB-{suffix}", fechaCierre: null);

        await SeedAsync(context, unidad, cargo, persona, estadoAbierta, combinado, ocupacion, vacante);

        try
        {
            var repo = new PuestoRepository(context);
            var todos = await repo.ListarDisponiblesAsync(default);

            // La exclusion es por Ocupacion vigente (primera condicion que
            // falla); el filtro de Vacante tampoco pasaria, pero alcanza
            // con uno para excluir.
            Assert.DoesNotContain(todos, p => p.Id == combinado.Id);
        }
        finally
        {
            await CleanupAsync(context, vacante, ocupacion, combinado, cargo, unidad, estadoAbierta, persona);
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
