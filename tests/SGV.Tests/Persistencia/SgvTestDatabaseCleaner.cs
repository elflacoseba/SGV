using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Truncates the transactional tables of the shared <c>sgv_test</c>
/// database between xUnit test sessions so leftover rows from a prior
/// run cannot poison subsequent fixtures.
///
/// Issue #260 root cause: prior runs accumulated 200+ personas, 60+
/// cargos, etc., causing several Setup/Auth-gateway tests to fail with
/// pre-existing data conflicts. <see cref="MySqlTestDatabaseBootstrap"/>
/// already applies migrations once per session but never deletes the
/// rows those migrations create.
///
/// Catalog and seed tables (AspNetRoles, NivelesHabilidad, NivelesCargo,
/// Cargos, Habilidades, TiposUnidadOrganizativa, TiposDocumento,
/// CategoriasHabilidad, EstadosPostulacion, EstadosVacante) are
/// preserved because tests rely on them and the catalog GUIDs are stable
/// per <c>docs/decisiones-implementacion.md</c> §"Mapa de bloques GUID
/// reservados por catálogo".
/// </summary>
internal static class SgvTestDatabaseCleaner
{
    /// <summary>
    /// Tables holding rows that tests produce. Ordered so child rows are
    /// removed before parents (FK-safe even without
    /// <c>SET FOREIGN_KEY_CHECKS=0</c>).
    /// </summary>
    private static readonly string[] TransactionalTables =
    [
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserRoles",
        "AspNetUserTokens",
        "PersonaHabilidades",
        "CargoHabilidades",
        "EvaluacionesPostulacion",
        "HistorialEstadosPostulacion",
        "HistorialEstadosVacante",
        "Postulaciones",
        "Postulantes",
        "Ocupaciones",
        "Vacantes",
        "Puestos",
        "UnidadesOrganizativas",
        "AspNetUsers",
        "Personas",
        "Auditorias",
    ];

    public static async Task CleanAsync(SgvDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0", cancellationToken)
            .ConfigureAwait(false);
        try
        {
            foreach (var table in TransactionalTables)
            {
                await context.Database
                    .ExecuteSqlRawAsync($"DELETE FROM `{table}`", cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1", cancellationToken)
                .ConfigureAwait(false);
        }
    }
}