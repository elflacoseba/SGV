using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Auditoria;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Tests.Persistencia;
using Xunit;

namespace SGV.Tests.Aplicacion.Auditoria;

/// <summary>
/// Tests S1 del módulo de auditoría (consulta): comportamiento de filtros/orden,
/// contrato wire sin <c>OldValuesJson</c>/<c>NewValuesJson</c> y guardrail
/// de no-recursión de auditoría (D-4).
///
/// Tareas cubiertas:
///   1.1 — filtros omitidos no filtran; combinados sí; Id DESC en empates; DateFrom&gt;DateTo → ArgumentException.
///   1.2 — proyección wire sin <c>OldValuesJson</c>/<c>NewValuesJson</c> (tipo + serialización).
///   1.3 — threat-matrix: <c>QueryAsync</c> no inserta filas en <c>Auditorias</c>.
///
/// En STRICT TDD, este archivo es la fase RED: los tipos
/// <see cref="IAuditoriaServicioConsulta"/>, <see cref="AuditoriaListQuery"/>,
/// <see cref="AuditoriaDto"/> y <c>AuditoriaServicioConsulta</c> aún NO
/// existen; el archivo NO compila hasta que la fase GREEN los introduzca.
/// </summary>
public sealed class AuditoriaServicioConsultaTests
{
    private static readonly DateTime BaseTime =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    // ====================================================================
    // 1.1 — Filtros combinables, orden determinista, rango inválido
    // ====================================================================

    /// <summary>
    /// Parametrización del contrato de filtros (1.1.a + 1.1.b):
    /// filtros omitidos NO filtran; filtros combinados aplican AND.
    /// Cada fila siembra 5 filas y compara <c>TotalCount</c>.
    ///
    /// Fixture sembrada por <see cref="AuditoriaTestScope.SeedFixtureAsync"/>:
    ///   Row 1: Persona   + Alta         + u1   (BaseTime - 1)
    ///   Row 2: Persona   + Modificacion + u1   (BaseTime - 2)
    ///   Row 3: Persona   + BajaLogica   + u2   (BaseTime - 3)
    ///   Row 4: Cargo     + Alta         + u2   (BaseTime - 4)
    ///   Row 5: Habilidad + Modificacion + u3   (BaseTime - 5)
    /// </summary>
    [MySqlTheory]
    [InlineData(null,       null,           null, null, null, 5)] // sin filtros → todos
    [InlineData("Persona",  null,           null, null, null, 3)] // solo EntityName
    [InlineData(null,       "Alta",         null, null, null, 2)] // solo Operation
    [InlineData("Persona",  "Alta",         null, null, null, 1)] // EntityName + Operation combinado
    [InlineData("Persona",  "Modificacion", null, null, null, 1)] // otro combinado (distinto)
    [InlineData(null,       null,           null, null, "u1", 2)] // solo UserId
    [InlineData(null,       null,           null, null, "u3", 1)] // UserId con un único resultado
    public async Task QueryAsync_Filtros_AplicanSegunEsperado(
        string? entityName,
        string? operation,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? userId,
        int expectedCount)
    {
        await using var scope = await AuditoriaTestScope.CreateAsync();
        await scope.SeedFixtureAsync();

        var servicio = new AuditoriaServicioConsulta(scope.Context);

        var resultado = await servicio.QueryAsync(new AuditoriaListQuery(
            Page: 1,
            PageSize: 20,
            EntityName: entityName,
            Operation: operation,
            DateFrom: dateFrom,
            DateTo: dateTo,
            UserId: userId));

        Assert.Equal(expectedCount, resultado.TotalCount);
    }

    /// <summary>
    /// 1.1.c — Orden determinista: con dos filas compartiendo
    /// <c>OccurredAt</c>, el <c>Id</c> mayor aparece primero.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_ConEmpateOccurredAt_OrdenaPorIdDesc()
    {
        await using var scope = await AuditoriaTestScope.CreateAsync();

        var idMenor = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var idMayor = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        // Dos filas con el MISMO OccurredAt e Id distinto.
        await scope.InsertarAuditoriaAsync(new AuditoriaEntity
        {
            Id = idMenor,
            OccurredAt = BaseTime,
            EntityName = "Persona",
            EntityId = Guid.NewGuid().ToString(),
            Operation = "Alta",
            UserId = "u1",
            ChangedPropertiesJson = "[]"
        });
        await scope.InsertarAuditoriaAsync(new AuditoriaEntity
        {
            Id = idMayor,
            OccurredAt = BaseTime,
            EntityName = "Persona",
            EntityId = Guid.NewGuid().ToString(),
            Operation = "Alta",
            UserId = "u1",
            ChangedPropertiesJson = "[]"
        });

        var servicio = new AuditoriaServicioConsulta(scope.Context);
        var resultado = await servicio.QueryAsync(new AuditoriaListQuery(1, 20));

        Assert.Equal(2, resultado.TotalCount);
        Assert.Equal(idMayor, resultado.Items[0].Id);
        Assert.Equal(idMenor, resultado.Items[1].Id);
    }

    /// <summary>
    /// 1.1.d — <c>DateFrom &gt; DateTo</c> debe lanzar
    /// <see cref="ArgumentException"/> para que S2 mapee a 400.
    /// NO se devuelve un conjunto vacío.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_DateFromPosteriorADateTo_LanzaArgumentException()
    {
        await using var scope = await AuditoriaTestScope.CreateAsync();
        await scope.SeedFixtureAsync();

        var servicio = new AuditoriaServicioConsulta(scope.Context);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.QueryAsync(new AuditoriaListQuery(
                Page: 1,
                PageSize: 20,
                DateFrom: BaseTime.AddDays(5),
                DateTo: BaseTime.AddDays(1))));

        Assert.Contains("DateFrom", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 1.1.e — Clamp inferior de paginación (D-3): con
    /// <c>Page &lt; 1</c> y <c>PageSize &lt; MinPageSize</c>, el resultado
    /// expone <c>Page == 1</c> y <c>PageSize == MinPageSize</c>. Los
    /// clamps viven en <c>AuditoriaServicioConsulta.QueryAsync</c>
    /// (constantes <c>MinPageSize</c>/<c>MaxPageSize</c>); este test
    /// los hace observables en el <c>PagedResult</c> devuelto.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_ClampInferior_PageYPageSizeSeAjustanAlMinimo()
    {
        await using var scope = await AuditoriaTestScope.CreateAsync();
        await scope.SeedFixtureAsync();

        var servicio = new AuditoriaServicioConsulta(scope.Context);

        var resultado = await servicio.QueryAsync(new AuditoriaListQuery(
            Page: 0,
            PageSize: 0));

        Assert.Equal(1, resultado.Page);
        Assert.Equal(1, resultado.PageSize);
        Assert.Single(resultado.Items);
    }

    /// <summary>
    /// 1.1.f — Clamp superior de paginación (D-3): con
    /// <c>PageSize &gt; MaxPageSize</c>, el resultado expone
    /// <c>PageSize == MaxPageSize</c>. <c>Page</c> negativo se ajusta a 1.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_ClampSuperior_PageSizeSeAjustaAlMaximo()
    {
        await using var scope = await AuditoriaTestScope.CreateAsync();
        await scope.SeedFixtureAsync();

        var servicio = new AuditoriaServicioConsulta(scope.Context);

        var resultado = await servicio.QueryAsync(new AuditoriaListQuery(
            Page: -5,
            PageSize: 9999));

        Assert.Equal(1, resultado.Page);
        Assert.Equal(100, resultado.PageSize);
    }

    // ====================================================================
    // 1.2 — Contrato wire sin OldValuesJson / NewValuesJson
    // ====================================================================

    /// <summary>
    /// 1.2.a — Garantía estructural: el tipo <see cref="AuditoriaDto"/>
    /// NO expone <c>OldValuesJson</c> ni <c>NewValuesJson</c> como
    /// propiedades (compilación + reflexión).
    /// Test intencionalmente clasificado como <c>[Fact]</c> (no
    /// <c>[MySqlFact]</c>): no necesita una base MySQL porque sólo
    /// inspecciona metadatos del tipo wire.
    /// </summary>
    [Fact]
    public void AuditoriaDto_NoExponeOldValuesJsonNiNewValuesJson()
    {
        var tipo = typeof(AuditoriaDto);

        Assert.Null(tipo.GetProperty("OldValuesJson"));
        Assert.Null(tipo.GetProperty("NewValuesJson"));
    }

    /// <summary>
    /// 1.2.b — Garantía wire (listado): la serialización JSON de un
    /// DTO producido por <c>QueryAsync</c> no contiene las claves
    /// <c>OldValuesJson</c> ni <c>NewValuesJson</c>, pero sí
    /// <c>ChangedPropertiesJson</c> (control).
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_Proyeccion_NoContieneOldNewValuesEnSerializacion()
    {
        await using var scope = await AuditoriaTestScope.CreateAsync();
        await scope.InsertarAuditoriaAsync(new AuditoriaEntity
        {
            Id = Guid.NewGuid(),
            OccurredAt = BaseTime,
            EntityName = "Persona",
            EntityId = Guid.NewGuid().ToString(),
            Operation = "Modificacion",
            UserId = "u1",
            OldValuesJson = "{\"nombre\":\"viejo\"}",
            NewValuesJson = "{\"nombre\":\"nuevo\"}",
            ChangedPropertiesJson = "[\"Nombre\"]"
        });

        var servicio = new AuditoriaServicioConsulta(scope.Context);
        var resultado = await servicio.QueryAsync(new AuditoriaListQuery(1, 20));

        var dto = Assert.Single(resultado.Items);
        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("OldValuesJson", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NewValuesJson", json, StringComparison.Ordinal);
        Assert.Contains("ChangedPropertiesJson", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// 1.2.c — Garantía wire (detalle): la serialización del DTO de
    /// <c>GetByIdAsync</c> tampoco expone old/new values.
    /// </summary>
    [MySqlFact]
    public async Task GetByIdAsync_Proyeccion_NoContieneOldNewValuesEnSerializacion()
    {
        await using var scope = await AuditoriaTestScope.CreateAsync();
        var id = Guid.NewGuid();

        await scope.InsertarAuditoriaAsync(new AuditoriaEntity
        {
            Id = id,
            OccurredAt = BaseTime,
            EntityName = "Cargo",
            EntityId = Guid.NewGuid().ToString(),
            Operation = "Modificacion",
            UserId = "u1",
            OldValuesJson = "{\"a\":1}",
            NewValuesJson = "{\"a\":2}",
            ChangedPropertiesJson = "[\"Nombre\"]"
        });

        var servicio = new AuditoriaServicioConsulta(scope.Context);
        var dto = await servicio.GetByIdAsync(id);

        Assert.NotNull(dto);
        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("OldValuesJson", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NewValuesJson", json, StringComparison.Ordinal);
        Assert.Contains("ChangedPropertiesJson", json, StringComparison.Ordinal);
    }

    // ====================================================================
    // 1.3 — Threat-matrix: lectura no inserta auditoría (D-4)
    // ====================================================================

    /// <summary>
    /// 1.3 — Tras invocar <c>QueryAsync</c> sobre una base sembrada,
    /// la cantidad de filas en <c>Auditorias</c> debe ser exactamente
    /// la misma. Esto es el guardrail contra recursión: las
    /// consultas no disparan <c>SavingChanges</c>.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_NoInsertaAuditoriasNuevas()
    {
        await using var scope = await AuditoriaTestScope.CreateAsync();
        await scope.SeedFixtureAsync();

        var servicio = new AuditoriaServicioConsulta(scope.Context);

        var countAntes = await scope.Context.Auditorias.CountAsync();
        await servicio.QueryAsync(new AuditoriaListQuery(1, 20));
        await servicio.GetByIdAsync(scope.Context.Auditorias.First().Id);
        var countDespues = await scope.Context.Auditorias.CountAsync();

        Assert.Equal(countAntes, countDespues);
    }

    // ====================================================================
    // Test scope — base MySQL aislada por test (sin interceptor)
    // ====================================================================

    /// <summary>
    /// Crea una base de datos MySQL efímera con el esquema real de
    /// <see cref="SgvDbContext"/> (vía <c>EnsureCreated</c>). NO
    /// registra el <c>AuditoriaSaveChangesInterceptor</c> porque
    /// este test de lectura debe ejercitar el camino de no-auditoría
    /// (D-4) sin falsear el síntoma de "no inserta filas" por
    /// ausencia del interceptor.
    /// </summary>
    private sealed class AuditoriaTestScope : IAsyncDisposable
    {
        private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 36));

        private AuditoriaTestScope(SgvDbContext context)
        {
            Context = context;
        }

        public SgvDbContext Context { get; }

        public static async Task<AuditoriaTestScope> CreateAsync()
        {
            var databaseName = $"SGV_AuditoriaConsultaTests_{Guid.NewGuid():N}";
            var connectionString = TestSgvDbContextFactory.BuildConnectionStringForDatabase(databaseName);
            var options = new DbContextOptionsBuilder<SgvDbContext>()
                .UseMySql(connectionString, ServerVersion)
                .Options;

            var context = new SgvDbContext(options);
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            return new AuditoriaTestScope(context);
        }

        /// <summary>
        /// Fixture: 5 filas de auditoría distribuidas en entidades,
        /// operaciones, usuarios y fechas para sostener la
        /// parametrización de filtros (ver comentarios arriba).
        /// </summary>
        public async Task SeedFixtureAsync()
        {
            await Context.Auditorias.AddRangeAsync(
                MakeRow("Persona",   "Alta",         "u1", BaseTime.AddDays(-1)),
                MakeRow("Persona",   "Modificacion", "u1", BaseTime.AddDays(-2)),
                MakeRow("Persona",   "BajaLogica",   "u2", BaseTime.AddDays(-3)),
                MakeRow("Cargo",     "Alta",         "u2", BaseTime.AddDays(-4)),
                MakeRow("Habilidad", "Modificacion", "u3", BaseTime.AddDays(-5)));
            await Context.SaveChangesAsync();
        }

        public async Task InsertarAuditoriaAsync(AuditoriaEntity row)
        {
            await Context.Auditorias.AddAsync(row);
            await Context.SaveChangesAsync();
        }

        private static AuditoriaEntity MakeRow(
            string entity, string operacion, string user, DateTime occurredAt) =>
            new()
            {
                Id = Guid.NewGuid(),
                EntityName = entity,
                EntityId = Guid.NewGuid().ToString(),
                Operation = operacion,
                UserId = user,
                OccurredAt = occurredAt,
                ChangedPropertiesJson = "[]"
            };

        public async ValueTask DisposeAsync()
        {
            await Context.Database.EnsureDeletedAsync();
            await Context.DisposeAsync();
        }
    }
}