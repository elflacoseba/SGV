using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class HabilidadRepository(SgvDbContext context)
    : ReadOnlyRepository<HabilidadEntity, Habilidad>(context), IHabilidadRepository
{
    protected override IQueryable<HabilidadEntity> Query => base
        .Query
        .Where(h => h.IsActive);

    protected override Habilidad MapToDomain(HabilidadEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    public override async Task<IReadOnlyList<Habilidad>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query
            .OrderBy(h => h.Codigo)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }

    // ── Métodos de escritura ───────────────────────────────────────
    // Stubs de la fase 1 (slice 1) del cambio `implementa-modulo-habilidades`.
    // La implementación real con EF Core/Pomelo llega en la fase 2 (slice 2)
    // y reemplaza cada NotImplementedException por la consulta correspondiente.

    public Task AddAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "HabilidadRepository.AddAsync será implementado en el slice 2 (persistencia MySQL/Pomelo).");
    }

    public Task<Habilidad?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "HabilidadRepository.GetByIdForUpdateAsync será implementado en el slice 2 (persistencia MySQL/Pomelo).");
    }

    public Task<Habilidad?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "HabilidadRepository.GetByIdIncludingDeletedAsync será implementado en el slice 2 (persistencia MySQL/Pomelo).");
    }

    public Task UpdateAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "HabilidadRepository.UpdateAsync será implementado en el slice 2 (persistencia MySQL/Pomelo).");
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "HabilidadRepository.DeleteAsync será implementado en el slice 2 (persistencia MySQL/Pomelo).");
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "HabilidadRepository.ReactivateAsync será implementado en el slice 2 (persistencia MySQL/Pomelo).");
    }

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "HabilidadRepository.ExistsActiveCodeAsync será implementado en el slice 2 (persistencia MySQL/Pomelo).");
    }
}
