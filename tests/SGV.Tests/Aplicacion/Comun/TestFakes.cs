using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Dominio.Habilidades;

namespace SGV.Tests.Aplicacion.Comun;

internal sealed class FakeHabilidadReadRepository : IHabilidadRepository
{
    private readonly Habilidad? _habilidad;

    public FakeHabilidadReadRepository() { }

    public FakeHabilidadReadRepository(Habilidad habilidad)
    {
        _habilidad = habilidad;
    }

    public Task<Habilidad?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = _habilidad is not null && _habilidad.Id == id && _habilidad.IsActive && !_habilidad.IsDeleted
            ? _habilidad
            : null;
        return Task.FromResult(match);
    }

    public Task<Habilidad?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_habilidad?.Id == id && _habilidad.IsActive && !_habilidad.IsDeleted ? _habilidad : null);

    public Task<IReadOnlyList<Habilidad>> ListAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Habilidad>>(_habilidad is null ? [] : [_habilidad]);

    public Task AddAsync(Habilidad h, CancellationToken ct = default) => Task.CompletedTask;
    public Task UpdateAsync(Habilidad h, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    public Task ReactivateAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    public Task<Habilidad?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_habilidad?.Id == id ? _habilidad : null);
    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken ct = default)
        => Task.FromResult(false);
}

internal sealed class FakeNivelHabilidadRepo : INivelHabilidadRepository
{
    private readonly NivelHabilidad? _nivel;

    public FakeNivelHabilidadRepo() { }

    public FakeNivelHabilidadRepo(NivelHabilidad nivel)
    {
        _nivel = nivel;
    }

    public Task<NivelHabilidad?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = _nivel is not null && _nivel.Id == id ? _nivel : null;
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<NivelHabilidad>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NivelHabilidad>>(_nivel is null ? [] : [_nivel]);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.FromResult(1);
    }
}
