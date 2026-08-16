using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Fake en memoria de <see cref="IUnidadOrganizativaApiClient"/> usado por las
/// pruebas web de Puestos PR 3A para cargar el dropdown de unidades
/// organizativas. <see cref="QueryAsync"/> cubre el camino paginado que el
/// resto de los módulos todavía consume; <see cref="GetAllActivasAsync"/>
/// cubre el nuevo contrato de la página Create de Puestos, que requiere
/// el catálogo completo sin truncar (issue #103). Los demás métodos de
/// la interfaz lanzan <see cref="NotImplementedException"/> con un mensaje
/// ruidoso para que cualquier test que olvide cablearlos falle de forma
/// explícita en vez de devolver datos basura.
/// </summary>
public sealed class FakeUnidadOrganizativaApiClient : IUnidadOrganizativaApiClient
{
    /// <summary>
    /// Resultado que devolverá <see cref="QueryAsync"/>. Por defecto, lista
    /// vacía con <c>PageSize=200</c> para preservar el contrato histórico
    /// que asumían las pruebas pre-existentes del módulo.
    /// </summary>
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

    /// <summary>Resultado que devolverá <see cref="GetAllActivasAsync"/>. Por defecto, lista vacía.</summary>
    public IReadOnlyList<UnidadOrganizativaDto> AllActivasResult { get; set; } = [];

    /// <summary>Excepción opcional que <see cref="GetAllActivasAsync"/> debe lanzar.</summary>
    public Exception? AllActivasException { get; set; }

    /// <summary>Page sizes recibidos vía <see cref="GetAllActivasAsync"/>.</summary>
    public List<int> GetAllActivasCalls { get; } = [];

    public Task<IReadOnlyList<UnidadOrganizativaDto>> GetAllActivasAsync(int pageSize = 100, CancellationToken cancellationToken = default)
    {
        GetAllActivasCalls.Add(pageSize);

        if (AllActivasException is not null)
        {
            return Task.FromException<IReadOnlyList<UnidadOrganizativaDto>>(AllActivasException);
        }

        return Task.FromResult(AllActivasResult);
    }

    public Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException($"FakeUnidadOrganizativaApiClient.GetByIdAsync({id}) no está cableado.");

    public Task<UnidadOrganizativaArbolResponse> GetTreeAsync(CancellationToken cancellationToken = default)
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
