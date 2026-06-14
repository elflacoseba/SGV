using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Dominio.Organizacion;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class UnidadOrganizativaRepository(SgvDbContext context)
    : ReadOnlyRepository<UnidadOrganizativa>(context), IUnidadOrganizativaRepository
{
    protected override IQueryable<UnidadOrganizativa> Query => base
        .Query
        .Where(u => u.IsActive);

    public override async Task<IReadOnlyList<UnidadOrganizativa>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await Query
            .OrderBy(u => u.Codigo)
            .ToListAsync(cancellationToken);
    }
}
