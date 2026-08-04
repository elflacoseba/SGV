using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Auditoria;

namespace SGV.Tests.Web.Auditoria;

/// <summary>
/// Fake en memoria de <see cref="IAuditoriaApiClient"/> compartido
/// por la suite web del módulo de Auditoría. Permite configurar
/// resultados de <see cref="QueryAsync"/> y
/// <see cref="GetDetalleAsync"/>, forzar excepciones y registrar
/// invocaciones para que los PageModel seam tests verifiquen la
/// propagación de filtros via PRG.
/// </summary>
public sealed class FakeAuditoriaApiClient : IAuditoriaApiClient
{
    /// <summary>
    /// Handler opcional que permite personalizar el resultado de
    /// cada consulta paginada en base al <see cref="AuditoriaListQuery"/>
    /// recibido. Si no se configura, el fake devuelve
    /// <see cref="QueryResult"/>.
    /// </summary>
    public Func<AuditoriaListQuery, PagedResult<AuditoriaDto>>? QueryHandler { get; set; }

    /// <summary>
    /// Resultado de <see cref="QueryAsync"/> cuando no hay override.
    /// Default: página vacía con paginación 1/20.
    /// </summary>
    public PagedResult<AuditoriaDto> QueryResult { get; set; } =
        new(Array.Empty<AuditoriaDto>(), 0, 1, 20);

    /// <summary>
    /// Excepción opcional que <see cref="QueryAsync"/> debe lanzar
    /// (simula una falla de transporte contra el backend).
    /// </summary>
    public Exception? QueryException { get; set; }

    /// <summary>
    /// Resultado de <see cref="GetDetalleAsync"/> cuando no hay
    /// override por id. Default: <c>null</c> (404 simulado).
    /// </summary>
    public AuditoriaDetalleDto? GetDetalleResult { get; set; }

    /// <summary>
    /// Handler opcional para <see cref="GetDetalleAsync"/>. Si está
    /// seteado, tiene prioridad sobre <see cref="GetDetalleResult"/>.
    /// </summary>
    public Func<Guid, AuditoriaDetalleDto?>? GetDetalleHandler { get; set; }

    /// <summary>
    /// Excepción opcional que <see cref="GetDetalleAsync"/> debe
    /// lanzar (simula una falla de transporte contra el backend al
    /// solicitar el detalle). Tiene prioridad sobre
    /// <see cref="GetDetalleHandler"/> y <see cref="GetDetalleResult"/>.
    /// </summary>
    public Exception? GetDetalleException { get; set; }

    /// <summary>Captura de invocaciones de <see cref="QueryAsync"/>.</summary>
    public List<AuditoriaListQuery> QueryCalls { get; } = [];

    /// <summary>Captura de invocaciones de <see cref="GetDetalleAsync"/>.</summary>
    public List<Guid> GetDetalleCalls { get; } = [];

    /// <summary>
    /// Resultado de <see cref="GetFilterOptionsAsync"/> cuando no hay
    /// override. Default: <see cref="AuditoriaFilterOptions"/> con
    /// ambas colecciones vacías (cubre los tests donde el PageModel
    /// debe manejar una respuesta vacía sin crashear).
    /// </summary>
    public AuditoriaFilterOptions GetFilterOptionsResult { get; set; } =
        new(Array.Empty<string>(), Array.Empty<string>());

    /// <summary>
    /// Handler opcional para <see cref="GetFilterOptionsAsync"/>.
    /// Tiene prioridad sobre <see cref="GetFilterOptionsResult"/>
    /// (mismo patrón que <see cref="GetDetalleHandler"/>).
    /// </summary>
    public Func<AuditoriaFilterOptions>? GetFilterOptionsHandler { get; set; }

    /// <summary>
    /// Excepción opcional que <see cref="GetFilterOptionsAsync"/>
    /// debe lanzar (simula una falla de transporte del endpoint
    /// <c>GET /api/v1/auditorias/filter-options</c> para que el
    /// PageModel active la rama de fallback a inputs de texto).
    /// Tiene prioridad sobre <see cref="GetFilterOptionsHandler"/> y
    /// <see cref="GetFilterOptionsResult"/>.
    /// </summary>
    public Exception? GetFilterOptionsException { get; set; }

    /// <summary>Captura de invocaciones de <see cref="GetFilterOptionsAsync"/>.</summary>
    public List<int> GetFilterOptionsCalls { get; } = [];

    public Task<PagedResult<AuditoriaDto>> QueryAsync(
        AuditoriaListQuery query,
        CancellationToken cancellationToken = default)
    {
        QueryCalls.Add(query);

        if (QueryException is not null)
        {
            throw QueryException;
        }

        if (QueryHandler is not null)
        {
            return Task.FromResult(QueryHandler(query));
        }

        return Task.FromResult(QueryResult);
    }

    public Task<AuditoriaDetalleDto?> GetDetalleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        GetDetalleCalls.Add(id);

        if (GetDetalleException is not null)
        {
            throw GetDetalleException;
        }

        if (GetDetalleHandler is not null)
        {
            return Task.FromResult(GetDetalleHandler(id));
        }

        return Task.FromResult(GetDetalleResult);
    }

    public Task<AuditoriaFilterOptions> GetFilterOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        GetFilterOptionsCalls.Add(1);

        if (GetFilterOptionsException is not null)
        {
            throw GetFilterOptionsException;
        }

        if (GetFilterOptionsHandler is not null)
        {
            return Task.FromResult(GetFilterOptionsHandler());
        }

        return Task.FromResult(GetFilterOptionsResult);
    }
}
