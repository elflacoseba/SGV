using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Fake en memoria de <see cref="IUnidadOrganizativaApiClient"/> usado por las
/// pruebas web de Puestos PR 3A para cargar el dropdown de unidades
/// organizativas. Sólo expone <c>QueryAsync</c> como respuesta programada
/// (la única vía real del catálogo en este slice); los demás métodos de la
/// interfaz lanzan <see cref="NotImplementedException"/> con un mensaje
/// ruidoso para que cualquier test que olvide cablearlos falle de forma
/// explícita en vez de devolver datos basura.
/// </summary>
public sealed class FakeUnidadOrganizativaApiClient : IUnidadOrganizativaApiClient
{
    /// <summary>Resultado que devolverá <see cref="QueryAsync"/>. Por defecto, lista vacía.</summary>
    public PagedResult<UnidadOrganizativaDto> QueryResult { get; set; } =
        new(Array.Empty<UnidadOrganizativaDto>(), 0, 1, 200);

    /// <summary>Excepción opcional que <see cref="QueryAsync"/> debe lanzar.</summary>
    public Exception? QueryException { get; set; }

    /// <summary>Consultas recibidas vía <see cref="QueryAsync"/>.</summary>
    public List<UnidadOrganizativaListQuery> QueryCalls { get; } = new();

    public Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(UnidadOrganizativaListQuery query, CancellationToken cancellationToken = default)
    {
        QueryCalls.Add(query);

        if (QueryException is not null)
        {
            return Task.FromException<PagedResult<UnidadOrganizativaDto>>(QueryException);
        }

        return Task.FromResult(QueryResult);
    }

    public Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException($"FakeUnidadOrganizativaApiClient.GetByIdAsync({id}) no está cableado.");

    public Task<IReadOnlyList<UnidadOrganizativaTreeNodeDto>> GetTreeAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("FakeUnidadOrganizativaApiClient.GetTreeAsync no está cableado.");

    public Task<IReadOnlyList<TipoUnidadOrganizativaDto>> GetTiposAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("FakeUnidadOrganizativaApiClient.GetTiposAsync no está cableado.");

    public Task<UnidadOrganizativaCommandResult> CreateAsync(CrearUnidadOrganizativaRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("FakeUnidadOrganizativaApiClient.CreateAsync no está cableado.");

    public Task<UnidadOrganizativaCommandResult> UpdateAsync(Guid id, ActualizarUnidadOrganizativaRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("FakeUnidadOrganizativaApiClient.UpdateAsync no está cableado.");

    public Task<UnidadOrganizativaCommandResult> ChangeParentAsync(Guid id, CambiarUnidadPadreRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("FakeUnidadOrganizativaApiClient.ChangeParentAsync no está cableado.");

    public Task<UnidadOrganizativaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("FakeUnidadOrganizativaApiClient.DeleteAsync no está cableado.");

    public Task<UnidadOrganizativaCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("FakeUnidadOrganizativaApiClient.ReactivateAsync no está cableado.");
}
