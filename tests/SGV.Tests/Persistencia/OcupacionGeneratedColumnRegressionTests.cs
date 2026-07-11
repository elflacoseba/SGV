using Microsoft.EntityFrameworkCore;
using SGV.Dominio.Ocupaciones;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Canary tests for the ActivePuestoIdUnique generated column. Guards
/// against regressions of issue #59: the column MUST be char(36) to match
/// PuestoId, otherwise MySQL truncates the computed expression and rejects
/// every insert of an active OcupacionEntity. These tests run against a
/// real MySQL server (skipped locally when none is available).
/// </summary>
public sealed class OcupacionGeneratedColumnRegressionTests
{
    [MySqlFact]
    public async Task AddAsync_FilaActiva_ActivePuestoIdUniquePersisteComoGuidString()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var puestoId = Guid.NewGuid();
        var persona = RepositoryTestData.CreatePersona("OCUP-CANARY-PER");
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("OCUP-CANARY-UO");
        var cargo = RepositoryTestData.CreateCargo("OCUP-CANARY-CARGO");
        var puesto = RepositoryTestData.CreatePuesto("OCUP-CANARY-PUE", unidad, cargo);
        puesto.Id = puestoId;

        var entity = new OcupacionEntity
        {
            Id = Guid.NewGuid(),
            PersonaId = persona.Id,
            PuestoId = puestoId,
            FechaInicio = new DateOnly(2026, 7, 11),
            FechaFin = null,
            TipoAsignacion = TipoAsignacion.Permanente,
            Observaciones = "OCUP-CANARY-1",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            context.Set<PersonaEntity>().Add(persona);
            context.Set<UnidadOrganizativaEntity>().Add(unidad);
            context.Set<CargoEntity>().Add(cargo);
            context.Set<PuestoEntity>().Add(puesto);
            context.Set<OcupacionEntity>().Add(entity);
            await context.SaveChangesAsync();

            // Read the generated column via raw SQL — the property is shadow
            // and not loaded by the tracked entity.
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `ActivePuestoIdUnique` FROM `Ocupaciones` WHERE `Id` = @id";
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "@id";
            idParameter.Value = entity.Id.ToString();
            command.Parameters.Add(idParameter);

            var result = await command.ExecuteScalarAsync();

            Assert.NotNull(result);
            Assert.Equal(puestoId.ToString(), (string)result!);
        }
        finally
        {
            // Best-effort cleanup. If the connection is broken (test failed
            // mid-flight, MySQL shutdown, etc.) the cleanup SaveChangesAsync
            // would throw and mask the original assertion failure. Swallow
            // cleanup exceptions and log them so the test result reflects the
            // assertion, not the cleanup.
            try
            {
                context.Set<OcupacionEntity>().Remove(entity);
                context.Set<PuestoEntity>().Remove(puesto);
                context.Set<CargoEntity>().Remove(cargo);
                context.Set<UnidadOrganizativaEntity>().Remove(unidad);
                context.Set<PersonaEntity>().Remove(persona);
                await context.SaveChangesAsync();
            }
            catch (Exception cleanupEx)
            {
                // Surface the cleanup failure but don't override the test result.
                // In CI, this appears as a warning in the test log.
                Console.WriteLine(
                    $"[Cleanup Warning] Failed to remove test data: {cleanupEx.Message}");
            }
        }
    }
}
