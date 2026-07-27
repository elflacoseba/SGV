using System.Net;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Fake en memoria de <see cref="IPuestosApiClient"/> compartido por las
/// pruebas web de Puestos. Decisión de diseño D2: respuestas programadas vía
/// propiedades (<c>GetAllResult</c>, <c>GetByIdResult</c>, <c>CreateResult</c>,
/// …) más captura de invocaciones (<c>GetAllCalls</c>, <c>DeleteCalls</c>, …) y
/// excepciones inyectables por método. Modela la baja lógica marcando ids
/// eliminados para que <c>GetAllAsync</c>/<c>GetByIdAsync</c> reflejen el
/// comportamiento real del backend.
/// </summary>
public sealed class FakePuestosApiClient : IPuestosApiClient
{
    private readonly HashSet<Guid> _deletedIds = new();

    // ── Respuestas programadas ──────────────────────────────────

    /// <summary>Resultado de <see cref="GetAllAsync"/> (se filtran los ids eliminados).</summary>
    public IReadOnlyList<PuestoDto> GetAllResult { get; set; } = [];

    /// <summary>
    /// Permite personalizar el resultado de cada consulta paginada. Cuando no
    /// se configura, el fake aplica segmento, búsqueda, orden y paginación sobre
    /// <see cref="GetAllResult"/>.
    /// </summary>
    public Func<PuestoListQuery, PagedResult<PuestoDto>>? QueryHandler { get; set; }

    /// <summary>Consultas recibidas por <see cref="QueryAsync"/>.</summary>
    public List<PuestoListQuery> QueryCalls { get; } = [];

    /// <summary>Excepción opcional que <see cref="QueryAsync"/> debe lanzar.</summary>
    public Exception? QueryException { get; set; }

    /// <summary>Resultado de <see cref="GetByIdAsync"/> cuando no se resuelve desde <see cref="GetAllResult"/>.</summary>
    public PuestoDto? GetByIdResult { get; set; }

    /// <summary>Resultado de <see cref="DeleteAsync"/>. Por defecto, éxito 204.</summary>
    public PuestoDeleteResult DeleteResult { get; set; } = new(true, HttpStatusCode.NoContent, null, null);

    /// <summary>Resultado de <see cref="CreateAsync"/>. Por defecto, fallo NotImplemented para forzar cableado explícito.</summary>
    public PuestoCommandResult CreateResult { get; set; } = PuestoCommandResult.Failure(
        new PuestoError(PuestoErrorType.NotFound, "NotImplemented", "CreateResult no fue cableado en el fake."));

    /// <summary>Resultado de <see cref="UpdateAsync"/>. Por defecto, fallo NotImplemented para forzar cableado explícito.</summary>
    public PuestoCommandResult UpdateResult { get; set; } = PuestoCommandResult.Failure(
        new PuestoError(PuestoErrorType.NotFound, "NotImplemented", "UpdateResult no fue cableado en el fake."));

    /// <summary>Resultado de <see cref="ReactivateAsync"/>. Por defecto, fallo NotImplemented para forzar cableado explícito.</summary>
    public PuestoCommandResult ReactivateResult { get; set; } = PuestoCommandResult.Failure(
        new PuestoError(PuestoErrorType.NotFound, "NotImplemented", "ReactivateResult no fue cableado en el fake."));

    // ── Excepciones inyectables ─────────────────────────────────

    public Exception? GetAllException { get; set; }
    public Exception? GetByIdException { get; set; }
    public Exception? CreateException { get; set; }
    public Exception? UpdateException { get; set; }
    public Exception? DeleteException { get; set; }
    public Exception? ReactivateException { get; set; }

    // ── Captura de invocaciones ─────────────────────────────────

    public List<int> GetAllCalls { get; } = new();
    public List<Guid> GetByIdCalls { get; } = new();
    public List<CrearPuestoRequest> CreateCalls { get; } = new();
    public List<(Guid Id, ActualizarPuestoRequest Request)> UpdateCalls { get; } = new();
    public List<Guid> DeleteCalls { get; } = new();
    public List<Guid> ReactivateCalls { get; } = new();

    /// <summary>Construye un fake que devuelve la lista indicada en <see cref="GetAllAsync"/>.</summary>
    public static FakePuestosApiClient WithPuestoList(params PuestoDto[] puestos)
        => new() { GetAllResult = puestos };

    /// <summary>Indica si el id fue marcado como eliminado vía <see cref="DeleteAsync"/>.</summary>
    public bool IsDeleted(Guid id) => _deletedIds.Contains(id);

    public Task<IReadOnlyList<PuestoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls.Add(1);

        if (GetAllException is not null)
        {
            throw GetAllException;
        }

        IReadOnlyList<PuestoDto> snapshot = GetAllResult;
        if (_deletedIds.Count > 0)
        {
            snapshot = snapshot.Where(p => !_deletedIds.Contains(p.Id)).ToArray();
        }

        return Task.FromResult(snapshot);
    }

    public Task<PagedResult<PuestoDto>> QueryAsync(
        PuestoListQuery query,
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

        var source = GetAllResult.AsEnumerable();
        source = query.Segmento == PuestoSegmentoListado.Eliminadas
            ? source.Where(p => _deletedIds.Contains(p.Id))
            : source.Where(p => !_deletedIds.Contains(p.Id));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(p =>
                p.Codigo.Contains(search, StringComparison.OrdinalIgnoreCase)
                || p.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (p.Descripcion?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = query.Sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => source.OrderByDescending(p => p.Codigo, StringComparer.OrdinalIgnoreCase),
            "nombre_desc" => source.OrderByDescending(p => p.Nombre, StringComparer.OrdinalIgnoreCase),
            "nombre_asc" => source.OrderBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase),
            _ => source.OrderBy(p => p.Codigo, StringComparer.OrdinalIgnoreCase)
        };

        var materialized = ordered.ToArray();
        var pageSize = Math.Max(1, query.PageSize);
        var page = Math.Max(1, query.Page);
        var items = materialized
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(new PagedResult<PuestoDto>(items, materialized.Length, page, pageSize));
    }

    public Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCalls.Add(id);

        if (GetByIdException is not null)
        {
            throw GetByIdException;
        }

        if (_deletedIds.Contains(id))
        {
            return Task.FromResult<PuestoDto?>(null);
        }

        var fromList = GetAllResult.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(fromList ?? GetByIdResult);
    }

    public Task<PuestoCommandResult> CreateAsync(CrearPuestoRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return Task.FromResult(CreateResult);
    }

    public Task<PuestoCommandResult> UpdateAsync(Guid id, ActualizarPuestoRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((id, request));

        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<PuestoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);

        if (DeleteException is not null)
        {
            throw DeleteException;
        }

        if (DeleteResult.Succeeded)
        {
            _deletedIds.Add(id);
        }

        // El cliente real (PuestosApiClient.DeleteAsync) popula Categoria
        // desde StatusCode vía DeleteResultMapper → CommandResultMapper.Map.
        // El fake refleja el mismo comportamiento: cuando el programador de
        // tests setea StatusCode conocido y deja Categoria con su default
        // (`ErrorCategoria.NotFound`), inferimos la categoría equivalente.
        // Si el programador seteó Categoria explícitamente, se respeta.
        return Task.FromResult(ResolveDeleteCategoria(DeleteResult));
    }

    private static PuestoDeleteResult ResolveDeleteCategoria(PuestoDeleteResult result)
    {
        if (result.Succeeded || result.StatusCode is null || result.Categoria != default)
        {
            return result;
        }

        var categoria = (int)result.StatusCode.Value switch
        {
            400 or 422 => ErrorCategoria.Validation,
            401 => ErrorCategoria.Unauthorized,
            403 => ErrorCategoria.Forbidden,
            404 => ErrorCategoria.NotFound,
            408 => ErrorCategoria.Transport,
            409 => ErrorCategoria.Conflict,
            500 or 502 or 503 or 504 => ErrorCategoria.Transport,
            _ => ErrorCategoria.Unexpected
        };

        return result with { Categoria = categoria };
    }

    public Task<PuestoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCalls.Add(id);

        if (ReactivateException is not null)
        {
            throw ReactivateException;
        }

        if (ReactivateResult.IsSuccess)
        {
            _deletedIds.Remove(id);
        }

        return Task.FromResult(ReactivateResult);
    }
}
