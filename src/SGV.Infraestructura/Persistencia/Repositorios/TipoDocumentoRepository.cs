using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Read-only repository for the <c>TipoDocumento</c> catalog (issue #147).
/// No <c>IsActive</c> filter (catalog is immutable per REQ-SPA-EVOLUTION-001
/// condición #1). No <c>IsDeleted</c> filter (catalog does not have that
/// column).
/// </summary>
public sealed class TipoDocumentoRepository(SgvDbContext context) : ITipoDocumentoRepository
{
    private readonly SgvDbContext _context = context;

    public async Task<TipoDocumento?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<TipoDocumentoEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<TipoDocumento>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context
            .Set<TipoDocumentoEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }

    public async Task<TipoDocumento?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var entity = await _context
            .Set<TipoDocumentoEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Codigo == codigo, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }
}
