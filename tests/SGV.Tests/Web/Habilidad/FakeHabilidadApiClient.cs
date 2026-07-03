using System.Net;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Habilidades;
using HabilidadListQuery = SGV.Web.Integration.Habilidades.HabilidadListQuery;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Fake en memoria de <see cref="IHabilidadApiClient"/> compartido por las
/// pruebas web de Habilidades. Permite configurar resultados de cada método,
/// forzar excepciones y registrar invocaciones.
/// </summary>
public sealed class FakeHabilidadApiClient : IHabilidadApiClient
{
    private readonly IReadOnlyList<HabilidadDto>? _getAllResult;

    public FakeHabilidadApiClient()
        : this(Array.Empty<HabilidadDto>())
    {
    }

    private FakeHabilidadApiClient(IReadOnlyList<HabilidadDto>? getAllResult)
    {
        _getAllResult = getAllResult;
    }

    public List<int> GetAllCalls { get; } = new();

    public List<Guid> DeleteCalls { get; } = new();

    public HabilidadDeleteResult DeleteResult { get; set; } = new(true, HttpStatusCode.NoContent, null, null);

    public HabilidadCommandResult CreateResult { get; set; } = HabilidadCommandResult.Failure(
        new HabilidadError(HabilidadErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    public List<CrearHabilidadRequest> CreateCalls { get; } = new();

    public Exception? CreateException { get; set; }

    public HabilidadCommandResult UpdateResult { get; set; } = HabilidadCommandResult.Failure(
        new HabilidadError(HabilidadErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    public List<(Guid Id, ActualizarHabilidadRequest Request)> UpdateCalls { get; } = new();

    public Exception? UpdateException { get; set; }

    public IReadOnlyList<NivelHabilidadDto> NivelesResult { get; set; } = [];

    public Exception? NivelesException { get; set; }

    public int NivelesCalls { get; private set; }

    public Func<HabilidadListQuery, PagedResult<HabilidadDto>>? QueryHandler { get; set; }

    public List<HabilidadListQuery> QueryCalls { get; } = new();

    public Exception? QueryException { get; set; }

    public HabilidadCommandResult ReactivateResult { get; set; } = HabilidadCommandResult.Success(
        new HabilidadDto(Guid.NewGuid(), "PROG", "Programación", null, "Técnica"));

    public List<Guid> ReactivateCalls { get; } = new();

    public Exception? ReactivateException { get; set; }

    public static FakeHabilidadApiClient WithHabilidadList(params HabilidadDto[] habilidades)
        => new(habilidades.Length == 0 ? null : habilidades);

    public Task<IReadOnlyList<HabilidadDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls.Add(1);
        return Task.FromResult<IReadOnlyList<HabilidadDto>>(_getAllResult ?? []);
    }

    public Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_getAllResult is null)
            return Task.FromResult<HabilidadDto?>(null);

        return Task.FromResult(_getAllResult.FirstOrDefault(c => c.Id == id));
    }

    public Task<HabilidadDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);
        return Task.FromResult(DeleteResult);
    }

    public Task<HabilidadCommandResult> CreateAsync(CrearHabilidadRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return Task.FromResult(CreateResult);
    }

    public Task<HabilidadCommandResult> UpdateAsync(Guid id, ActualizarHabilidadRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((id, request));

        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<IReadOnlyList<NivelHabilidadDto>> GetNivelesHabilidadAsync(CancellationToken cancellationToken = default)
    {
        NivelesCalls++;

        if (NivelesException is not null)
        {
            throw NivelesException;
        }

        return Task.FromResult(NivelesResult);
    }

    public Task<PagedResult<HabilidadDto>> QueryAsync(HabilidadListQuery query, CancellationToken cancellationToken = default)
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

        // Comportamiento server-side simulado (paridad con FakeCargoApiClient):
        // filtro por segmento + búsqueda (case-insensitive) + sort + paginación.
        var snapshot = (_getAllResult ?? Array.Empty<HabilidadDto>()).ToList();

        var lowered = query.Search?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(lowered))
        {
            snapshot = snapshot
                .Where(h => h.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || h.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || (h.Categoria?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (h.Descripcion?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        snapshot = ApplySort(snapshot, query.Sort);

        var total = snapshot.Count;
        var pageItems = snapshot
            .Skip(Math.Max(0, (query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToList();

        return Task.FromResult(new PagedResult<HabilidadDto>(pageItems, total, query.Page, query.PageSize));
    }

    private static List<HabilidadDto> ApplySort(List<HabilidadDto> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => source.OrderByDescending(static h => h.Codigo, StringComparer.OrdinalIgnoreCase).ToList(),
            "codigo_asc" => source.OrderBy(static h => h.Codigo, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombre_desc" => source.OrderByDescending(static h => h.Nombre, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombre_asc" => source.OrderBy(static h => h.Nombre, StringComparer.OrdinalIgnoreCase).ToList(),
            "categoria_desc" => source.OrderByDescending(static h => h.Categoria ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "categoria_asc" => source.OrderBy(static h => h.Categoria ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => source.OrderBy(static h => h.Codigo, StringComparer.OrdinalIgnoreCase).ToList()
        };

    public Task<HabilidadCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCalls.Add(id);

        if (ReactivateException is not null)
        {
            throw ReactivateException;
        }

        return Task.FromResult(ReactivateResult);
    }
}