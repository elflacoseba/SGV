using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Seguridad;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Verifica que el interceptor de auditoría (<c>AuditoriaSaveChangesInterceptor</c>)
/// excluye la columna <c>TokenHash</c> del payload persistido en
/// <c>Auditorias</c> cuando un <see cref="SGV.Infraestructura.Persistencia.Entidades.RefreshTokenEntity"/>
/// es dado de alta. Cubre REQ-RTM-AUDIT-1 (spec block B):
/// <c>TokenHash</c> es sensible y NO debe aparecer en <c>NewValuesJson</c>;
/// los demás campos (<c>FamilyId</c>, <c>UserId</c>, <c>ExpiresAt</c>,
/// <c>ReplacedById</c>) SÍ deben auditarse normalmente.
/// </summary>
/// <remarks>
/// Mecanismo vigente (sin código nuevo): <c>EsCampoSensible</c> en
/// <c>AuditoriaSaveChangesInterceptor</c> filtra por substring
/// <c>"Token"</c>, lo que captura <c>TokenHash</c> automáticamente. El
/// nombre <c>ReplacedById</c> evita el substring deliberadamente para no
/// ser filtrado (corrección del design, observación #1868).
/// </remarks>
public sealed class RefreshTokenAuditoriaTests
{
    [MySqlFact]
    public async Task AddAsync_NoIncluyeTokenHashEnNewValuesJson()
    {
        await using var scope = await AuditoriaRefreshTokenScope.CreateAsync();
        var context = scope.Context;

        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var familyId = fixture.TrackFamilyId(Guid.NewGuid());
        var snapshot = RefreshTokenTestFixture.CrearSnapshotValido(
            userId: fixture.UserId,
            familyId: familyId,
            tokenHash: new string('x', 64));

        var repo = new RefreshTokenRepository(context);
        await repo.AddAsync(snapshot, default);
        await context.SaveChangesAsync();

        var auditoria = await context.Auditorias
            .Where(a => a.EntityName == "RefreshToken" && a.EntityId == snapshot.Id.ToString())
            .SingleAsync();

        Assert.NotNull(auditoria.NewValuesJson);
        Assert.DoesNotContain("TokenHash", auditoria.NewValuesJson!, StringComparison.Ordinal);
    }

    [MySqlFact]
    public async Task AddAsync_ElRestoDeLosCamposSeAuditaNormalmente()
    {
        await using var scope = await AuditoriaRefreshTokenScope.CreateAsync();
        var context = scope.Context;

        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var familyId = fixture.TrackFamilyId(Guid.NewGuid());
        var snapshot = RefreshTokenTestFixture.CrearSnapshotValido(
            userId: fixture.UserId,
            familyId: familyId,
            tokenHash: new string('y', 64));

        var repo = new RefreshTokenRepository(context);
        await repo.AddAsync(snapshot, default);
        await context.SaveChangesAsync();

        var auditoria = await context.Auditorias
            .Where(a => a.EntityName == "RefreshToken" && a.EntityId == snapshot.Id.ToString())
            .SingleAsync();

        Assert.NotNull(auditoria.NewValuesJson);
        var valores = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            auditoria.NewValuesJson!)!;

        // El campo UserId, FamilyId, ExpiresAt y ReplacedById (null en el
        // alta) deben estar en el payload porque no contienen el substring
        // "Token".
        Assert.True(valores.ContainsKey(nameof(RefreshTokenEntity.UserId)),
            $"NewValuesJson debe incluir {nameof(RefreshTokenEntity.UserId)}");
        Assert.True(valores.ContainsKey(nameof(RefreshTokenEntity.FamilyId)),
            $"NewValuesJson debe incluir {nameof(RefreshTokenEntity.FamilyId)}");
        Assert.True(valores.ContainsKey(nameof(RefreshTokenEntity.ExpiresAt)),
            $"NewValuesJson debe incluir {nameof(RefreshTokenEntity.ExpiresAt)}");
        Assert.True(valores.ContainsKey(nameof(RefreshTokenEntity.ReplacedById)),
            $"NewValuesJson debe incluir {nameof(RefreshTokenEntity.ReplacedById)}");
    }

    /// <summary>
    /// Ámbito de DbContext + interceptor de auditoría + repositorio para los
    /// tests de auditoría de refresh tokens. Usa el
    /// <c>AuditoriaSaveChangesInterceptor</c> real (mismo que en producción)
    /// para que el comportamiento de exclusión por nombre quede cubierto de
    /// extremo a extremo, sin stubs.
    /// </summary>
    private sealed class AuditoriaRefreshTokenScope : IAsyncDisposable
    {
        private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 36));

        public SgvDbContext Context { get; }

        private AuditoriaRefreshTokenScope(SgvDbContext context)
        {
            Context = context;
        }

        public static async Task<AuditoriaRefreshTokenScope> CreateAsync()
        {
            var databaseName = $"SGV_RtAudit_{Guid.NewGuid():N}";
            var connectionString = TestSgvDbContextFactory.BuildConnectionStringForDatabase(databaseName);
            var interceptor = new AuditoriaSaveChangesInterceptor(new FakeUsuarioActual("audit-rt-user"));
            var options = new DbContextOptionsBuilder<SgvDbContext>()
                .UseMySql(connectionString, ServerVersion)
                .AddInterceptors(interceptor)
                .Options;

            var context = new SgvDbContext(options);
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            return new AuditoriaRefreshTokenScope(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.Database.EnsureDeletedAsync();
            await Context.DisposeAsync();
        }
    }

    private sealed class FakeUsuarioActual(string? userId) : IUsuarioActual
    {
        public string? UserId { get; } = userId;
        public Guid? PersonaId => null;
        public IReadOnlyCollection<string> Roles => [];
        public Guid? CorrelationId => Guid.NewGuid();
    }
}