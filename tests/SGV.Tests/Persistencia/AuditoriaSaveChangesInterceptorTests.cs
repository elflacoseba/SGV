using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using SGV.Aplicacion.Seguridad;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class AuditoriaSaveChangesInterceptorTests
{
    [MySqlFact]
    public async Task SaveChangesAsync_AuditaAltaYConservaNombreLogicoSinSuffixEntity()
    {
        await using var scope = await AuditTestContextScope.CreateAsync();
        var context = scope.Context;
        var entity = new CargoEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"CARGO-ALTA-{Guid.NewGuid():N}"[..20],
            Nombre = "Cargo Alta",
            IsActive = true
        };

        await context.Cargos.AddAsync(entity);
        await context.SaveChangesAsync();

        var auditoria = await context.Auditorias.SingleAsync();

        Assert.Equal("audit-user", entity.CreatedByUserId);
        Assert.NotEqual(default, entity.CreatedAt);
        Assert.Equal("Alta", auditoria.Operation);
        Assert.Equal("Cargo", auditoria.EntityName);
        Assert.Equal(entity.Id.ToString(), auditoria.EntityId);
        Assert.Equal("audit-user", auditoria.UserId);
        Assert.Equal(AuditTestContextScope.CorrelationId, auditoria.CorrelationId);
        Assert.Null(auditoria.OldValuesJson);

        var newValues = DeserializeObject(auditoria.NewValuesJson);
        Assert.Equal(entity.Codigo, newValues[nameof(CargoEntity.Codigo)].GetString());
        Assert.Equal(entity.Nombre, newValues[nameof(CargoEntity.Nombre)].GetString());
    }

    [MySqlFact]
    public async Task SaveChangesAsync_AuditaModificacionYPropiedadesCambiadas()
    {
        await using var scope = await AuditTestContextScope.CreateAsync();
        var context = scope.Context;
        var entity = new CargoEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"CARGO-MOD-{Guid.NewGuid():N}"[..20],
            Nombre = "Cargo Original",
            IsActive = true
        };

        await context.Cargos.AddAsync(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Cargos.SingleAsync(x => x.Id == entity.Id);
        persisted.Nombre = "Cargo Modificado";

        await context.SaveChangesAsync();

        var auditoria = await context.Auditorias
            .OrderBy(x => x.OccurredAt)
            .LastAsync();

        Assert.Equal("audit-user", persisted.UpdatedByUserId);
        Assert.NotNull(persisted.UpdatedAt);
        Assert.Equal("Modificacion", auditoria.Operation);
        Assert.Equal("Cargo", auditoria.EntityName);

        var oldValues = DeserializeObject(auditoria.OldValuesJson);
        var newValues = DeserializeObject(auditoria.NewValuesJson);
        var changedProperties = DeserializeArray(auditoria.ChangedPropertiesJson);

        Assert.Equal("Cargo Original", oldValues[nameof(CargoEntity.Nombre)].GetString());
        Assert.Equal("Cargo Modificado", newValues[nameof(CargoEntity.Nombre)].GetString());
        Assert.Contains(nameof(CargoEntity.Nombre), changedProperties);
    }

    [MySqlFact]
    public async Task SaveChangesAsync_ConvierteDeleteEnBajaLogica()
    {
        await using var scope = await AuditTestContextScope.CreateAsync();
        var context = scope.Context;
        var entity = new CargoEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"CARGO-DEL-{Guid.NewGuid():N}"[..20],
            Nombre = "Cargo Baja",
            IsActive = true
        };

        await context.Cargos.AddAsync(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Cargos.SingleAsync(x => x.Id == entity.Id);
        context.Cargos.Remove(persisted);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var deleted = await context.Cargos.SingleAsync(x => x.Id == entity.Id);
        var auditoria = await context.Auditorias
            .OrderBy(x => x.OccurredAt)
            .LastAsync();

        Assert.True(deleted.IsDeleted);
        Assert.Equal("audit-user", deleted.DeletedByUserId);
        Assert.NotNull(deleted.DeletedAt);
        Assert.Equal("BajaLogica", auditoria.Operation);
        Assert.Equal("Cargo", auditoria.EntityName);
    }

    [MySqlFact]
    public async Task SaveChangesAsync_ExcluyeCamposSensiblesYNormalizaNombreLogicoEnTiposDePersistencia()
    {
        await using var scope = await AuditTestContextScope.CreateAsync();
        var context = scope.Context;
        var entity = new SensitiveAuditEntity
        {
            Id = Guid.NewGuid(),
            Name = "Inicial",
            ApiToken = "token-inicial"
        };

        await context.SensitiveAuditEntities.AddAsync(entity);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.SensitiveAuditEntities.SingleAsync(x => x.Id == entity.Id);
        persisted.Name = "Actualizado";
        persisted.ApiToken = "token-actualizado";

        await context.SaveChangesAsync();

        var auditoria = await context.Auditorias
            .OrderBy(x => x.OccurredAt)
            .LastAsync();

        var oldValues = DeserializeObject(auditoria.OldValuesJson);
        var newValues = DeserializeObject(auditoria.NewValuesJson);
        var changedProperties = DeserializeArray(auditoria.ChangedPropertiesJson);

        Assert.Equal("SensitiveAudit", auditoria.EntityName);
        Assert.DoesNotContain(nameof(SensitiveAuditEntity.ApiToken), oldValues.Keys);
        Assert.DoesNotContain(nameof(SensitiveAuditEntity.ApiToken), newValues.Keys);
        Assert.DoesNotContain(nameof(SensitiveAuditEntity.ApiToken), changedProperties);
        Assert.Contains(nameof(SensitiveAuditEntity.Name), changedProperties);
    }

    private static Dictionary<string, JsonElement> DeserializeObject(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json!)!;
    }

    private static string[] DeserializeArray(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        return JsonSerializer.Deserialize<string[]>(json!)!;
    }

    private sealed class AuditTestContextScope : IAsyncDisposable
    {
        private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 36));

        public static readonly Guid CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private AuditTestContextScope(AuditTestDbContext context)
        {
            Context = context;
        }

        public AuditTestDbContext Context { get; }

        public static async Task<AuditTestContextScope> CreateAsync()
        {
            var databaseName = $"SGV_AuditTests_{Guid.NewGuid():N}";
            var connectionString = $"Server=localhost;Port=3306;Database={databaseName};User=root;";
            var interceptor = new AuditoriaSaveChangesInterceptor(new FakeUsuarioActual("audit-user", CorrelationId));
            var options = new DbContextOptionsBuilder<AuditTestDbContext>()
                .UseMySql(connectionString, ServerVersion)
                .AddInterceptors(interceptor)
                .Options;

            var context = new AuditTestDbContext(options);
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            return new AuditTestContextScope(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.Database.EnsureDeletedAsync();
            await Context.DisposeAsync();
        }
    }

    private sealed class AuditTestDbContext(DbContextOptions<AuditTestDbContext> options) : DbContext(options)
    {
        public DbSet<CargoEntity> Cargos => Set<CargoEntity>();

        public DbSet<SensitiveAuditEntity> SensitiveAuditEntities => Set<SensitiveAuditEntity>();

        public DbSet<AuditoriaEntity> Auditorias => Set<AuditoriaEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CargoEntity>(builder =>
            {
                builder.ToTable("Cargos");
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.Property(x => x.Codigo).HasMaxLength(50).IsRequired();
                builder.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
                builder.Property(x => x.CreatedByUserId).HasMaxLength(450);
                builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
                builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
            });

            modelBuilder.Entity<SensitiveAuditEntity>(builder =>
            {
                builder.ToTable("SensitiveAuditEntities");
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
                builder.Property(x => x.ApiToken).HasMaxLength(200).IsRequired();
                builder.Property(x => x.CreatedByUserId).HasMaxLength(450);
                builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
                builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
            });

            modelBuilder.Entity<AuditoriaEntity>(builder =>
            {
                builder.ToTable("Auditorias");
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.Property(x => x.UserId).HasMaxLength(450);
                builder.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
                builder.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
                builder.Property(x => x.Operation).HasMaxLength(50).IsRequired();
                builder.Property(x => x.OldValuesJson).HasColumnType("longtext");
                builder.Property(x => x.NewValuesJson).HasColumnType("longtext");
                builder.Property(x => x.ChangedPropertiesJson).HasColumnType("longtext");
            });
        }
    }

    private sealed class FakeUsuarioActual(string? userId, Guid? correlationId) : IUsuarioActual
    {
        public string? UserId { get; } = userId;

        public Guid? PersonaId => null;

        public IReadOnlyCollection<string> Roles => [];

        public Guid? CorrelationId { get; } = correlationId;
    }

    private sealed class SensitiveAuditEntity : AuditableEntityBase
    {
        public string Name { get; set; } = string.Empty;

        public string ApiToken { get; set; } = string.Empty;
    }
}
