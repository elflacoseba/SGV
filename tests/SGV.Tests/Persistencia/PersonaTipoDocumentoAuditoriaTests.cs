using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

#pragma warning disable EF1002 // SQL crudo intencional en tests [MySqlFact]

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests [MySqlFact] de auditoría del cambio de <c>TipoDocumentoId</c> en
/// <c>Persona</c> (issue #147, escenarios D1-D2 del spec persona-management).
///
/// El interceptor centralizado <c>AuditoriaSaveChangesInterceptor</c> registra
/// la transición en la tabla <c>Auditorias</c> con
/// <c>Entidad="Persona"</c>, <c>Operacion="Modificacion"</c>,
/// <c>ChangedPropertiesJson</c> conteniendo <c>"TipoDocumentoId"</c>, y los
/// valores anterior/nuevo en <c>OldValuesJson</c>/<c>NewValuesJson</c>.
/// </summary>
public sealed class PersonaTipoDocumentoAuditoriaTests : IAsyncLifetime
{
    private string _testDbName = null!;
    private string _testConnectionString = null!;

    public Task InitializeAsync()
    {
        _testDbName = $"sgv_tda_{Guid.NewGuid():N}"[..14];
        _testConnectionString = TestSgvDbContextFactory.BuildConnectionStringForDatabase(_testDbName);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var masterConn = new MySqlConnection(TestSgvDbContextFactory.ResolveConnectionString());
            await masterConn.OpenAsync();
            await using var cmd = masterConn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{_testDbName}`";
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private SgvDbContext CreateTestContext()
    {
        // El interceptor de auditoría NO está registrado por defecto en
        // SgvDbContext (es DI), así que hay que añadirlo explícitamente
        // para que estos tests verifiquen el flujo completo.
        var interceptor = new AuditoriaSaveChangesInterceptor(
            new FakeUsuarioActual("audit-user", Guid.NewGuid()));
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(_testConnectionString, ServerVersion.Parse("8.0.0-mysql"))
            .AddInterceptors(interceptor)
            .Options;
        return new SgvDbContext(options);
    }

    private sealed class FakeUsuarioActual(string userId, Guid correlationId)
        : SGV.Aplicacion.Seguridad.IUsuarioActual
    {
        public string? UserId => userId;
        public Guid? PersonaId => null;
        public IReadOnlyCollection<string> Roles => [];
        public Guid? CorrelationId => correlationId;
    }

    private async Task<Guid> InsertPersonaConTipoDocumentoAsync(Guid tipoDocumentoId, string numeroDocumento)
    {
        var id = Guid.NewGuid();
        await using var ctx = CreateTestContext();
        ctx.Personas.Add(new SGV.Infraestructura.Persistencia.Entidades.PersonaEntity
        {
            Id = id,
            Nombres = "Test",
            Apellidos = "Auditoria",
            Legajo = "LEG-AUD-" + id.ToString("N")[..8],
            Email = id.ToString("N") + "@audit.test",
            TipoDocumentoId = tipoDocumentoId == Guid.Empty ? null : tipoDocumentoId,
            NumeroDocumento = numeroDocumento,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [MySqlFact]
    public async Task CambiarTipoDocumento_DeDniAPasaporte_GeneraAuditoriaConCambio()
    {
        // Arrange: arrancar con el seed cargado y crear una Persona con DNI.
        await using var ctx0 = CreateTestContext();
        await ctx0.Database.MigrateAsync();

        var personaId = await InsertPersonaConTipoDocumentoAsync(
            TipoDocumentoConstantes.DniId, "12345678");

        // Act: cambiar TipoDocumentoId a Pasaporte.
        await using (var ctx = CreateTestContext())
        {
            var entity = await ctx.Personas.FirstAsync(p => p.Id == personaId);
            entity.TipoDocumentoId = TipoDocumentoConstantes.PasaporteId;
            entity.NumeroDocumento = "ABC123456";
            await ctx.SaveChangesAsync();
        }

        // Assert: la tabla Auditorias contiene una entrada con el cambio.
        await using var conn = new MySqlConnection(_testConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EntityName, Operation,
                   ChangedPropertiesJson,
                   OldValuesJson,
                   NewValuesJson
            FROM Auditorias
            WHERE EntityName = 'Persona' AND EntityId = @id
            ORDER BY OccurredAt DESC
            LIMIT 1";
        cmd.Parameters.AddWithValue("@id", personaId.ToString("D"));
        using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected at least one Auditoria row.");

        var entityName = reader.GetString(0);
        var operation = reader.GetString(1);
        var changedJson = reader.GetString(2);
        var oldJson = reader.GetString(3);
        var newJson = reader.GetString(4);

        Assert.Equal("Persona", entityName);
        Assert.Equal("Modificacion", operation);
        Assert.Contains("TipoDocumentoId", changedJson);
        Assert.Contains(TipoDocumentoConstantes.DniId.ToString("D"), oldJson);
        Assert.Contains(TipoDocumentoConstantes.PasaporteId.ToString("D"), newJson);
    }

    [MySqlFact]
    public async Task CambiarTipoDocumento_DeNullADni_GeneraAuditoria()
    {
        // Arrange: arrancar con el seed cargado y crear una Persona SIN tipo
        // (huérfana post-backfill dirty).
        await using var ctx0 = CreateTestContext();
        await ctx0.Database.MigrateAsync();

        var personaId = await InsertPersonaConTipoDocumentoAsync(Guid.Empty, "55555");

        // Act: cambiar a DNI.
        await using (var ctx = CreateTestContext())
        {
            var entity = await ctx.Personas.FirstAsync(p => p.Id == personaId);
            entity.TipoDocumentoId = TipoDocumentoConstantes.DniId;
            entity.NumeroDocumento = "12345678";
            await ctx.SaveChangesAsync();
        }

        // Assert: la transición NULL → DNI queda registrada.
        await using var conn = new MySqlConnection(_testConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT EntityName, Operation, ChangedPropertiesJson
            FROM Auditorias
            WHERE EntityName = 'Persona' AND EntityId = @id
            ORDER BY OccurredAt DESC
            LIMIT 1";
        cmd.Parameters.AddWithValue("@id", personaId.ToString("D"));
        using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected at least one Auditoria row.");

        var entityName = reader.GetString(0);
        var operation = reader.GetString(1);
        var changedJson = reader.GetString(2);

        Assert.Equal("Persona", entityName);
        Assert.Equal("Modificacion", operation);
        Assert.Contains("TipoDocumentoId", changedJson);
    }
}
