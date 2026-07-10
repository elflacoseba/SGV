using System.Net;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using CargoListQuery = SGV.Web.Integration.Organizacion.CargoListQuery;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Fake en memoria de <see cref="ICargoApiClient"/> compartido por las pruebas
/// web de Cargos. Permite configurar el resultado de <c>GetAllAsync</c>,
/// forzar excepciones, registrar las invocaciones y devolver un
/// <see cref="CargoDeleteResult"/> configurable desde cada test.
/// </summary>
public sealed class FakeCargoApiClient : ICargoApiClient
{
    private readonly IReadOnlyList<CargoDto>? _getAllResult;
    private readonly Exception? _getAllException;
    private readonly HashSet<Guid> _deletedIds = new();

    /// <summary>
    /// Construye un fake vacío. Útil para los tests del seam que sólo necesitan
    /// confirmar el override del servicio en el contenedor.
    /// </summary>
    public FakeCargoApiClient()
        : this(Array.Empty<CargoDto>(), null)
    {
    }

    private FakeCargoApiClient(IReadOnlyList<CargoDto>? getAllResult, Exception? getAllException)
    {
        _getAllResult = getAllResult;
        _getAllException = getAllException;
    }

    /// <summary>
    /// Cantidad de veces que se invocó <see cref="GetAllAsync"/>.
    /// </summary>
    public List<int> GetAllCalls { get; } = new();

    /// <summary>
    /// Identificadores enviados a <see cref="DeleteAsync"/>.
    /// </summary>
    public List<Guid> DeleteCalls { get; } = new();

    /// <summary>
    /// Resultado fijo que devolverá <see cref="DeleteAsync"/>. Por defecto,
    /// éxito con 204 NoContent.
    /// </summary>
    public CargoDeleteResult DeleteResult { get; set; } = new(true, HttpStatusCode.NoContent, null, null);

    /// <summary>
    /// Resultado fijo que devolverá <see cref="CreateAsync"/>. Por defecto,
    /// fallo de NotImplemented para forzar a los tests a configurarlo
    /// explícitamente cuando lo necesiten.
    /// </summary>
    public CargoCommandResult CreateResult { get; set; } = CargoCommandResult.Failure(
        new CargoError(CargoErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    /// <summary>
    /// Solicitudes recibidas por <see cref="CreateAsync"/>. Permite inspeccionar
    /// el payload enviado por la página al API en cada test.
    /// </summary>
    public List<CrearCargoRequest> CreateCalls { get; } = new();

    /// <summary>
    /// Excepción opcional que <see cref="CreateAsync"/> debe lanzar antes de
    /// devolver el resultado. Útil para simular errores de transporte.
    /// </summary>
    public Exception? CreateException { get; set; }

    /// <summary>
    /// Resultado fijo que devolverá <see cref="UpdateAsync"/>. Por defecto,
    /// fallo de NotImplemented para forzar a los tests a configurarlo
    /// explícitamente cuando lo necesiten.
    /// </summary>
    public CargoCommandResult UpdateResult { get; set; } = CargoCommandResult.Failure(
        new CargoError(CargoErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    /// <summary>
    /// Solicitudes recibidas por <see cref="UpdateAsync"/>. Permite inspeccionar
    /// el payload enviado por la página al API en cada test.
    /// </summary>
    public List<(Guid Id, ActualizarCargoRequest Request)> UpdateCalls { get; } = new();

    /// <summary>
    /// Excepción opcional que <see cref="UpdateAsync"/> debe lanzar antes de
    /// devolver el resultado. Útil para simular errores de transporte.
    /// </summary>
    public Exception? UpdateException { get; set; }

    /// <summary>
    /// Resultado fijo que devolverá <see cref="GetNivelesAsync"/>. Por defecto,
    /// lista vacía (el test debe configurarla cuando cargue la página Create).
    /// </summary>
    public IReadOnlyList<NivelCargoDto> NivelesResult { get; set; } = [];

    /// <summary>
    /// Excepción opcional que <see cref="GetNivelesAsync"/> debe lanzar. Útil
    /// para verificar el manejo de errores recuperables en OnGetAsync.
    /// </summary>
    public Exception? NivelesException { get; set; }

    /// <summary>
    /// Cantidad de veces que se invocó <see cref="GetNivelesAsync"/>.
    /// </summary>
    public int NivelesCalls { get; private set; }

    /// <summary>
    /// Resultado paginado que devolverá <see cref="QueryAsync"/>. Por defecto
    /// se calcula sobre <c>_getAllResult</c> aplicando el segmento
    /// (<c>activas</c> por defecto, <c>eliminadas</c> cuando
    /// <c>query.Status == "eliminadas"</c>) y la paginación.
    /// </summary>
    public Func<CargoListQuery, PagedResult<CargoDto>>? QueryHandler { get; set; }

    /// <summary>
    /// Solicitudes recibidas por <see cref="QueryAsync"/>.
    /// </summary>
    public List<CargoListQuery> QueryCalls { get; } = new();

    /// <summary>
    /// Excepción opcional que <see cref="QueryAsync"/> debe lanzar.
    /// </summary>
    public Exception? QueryException { get; set; }

    /// <summary>
    /// Resultado fijo que devolverá <see cref="ReactivateAsync"/>. Por defecto
    /// éxito con el DTO del primer cargo.
    /// </summary>
    public CargoCommandResult ReactivateResult { get; set; } = CargoCommandResult.Success(
        new CargoDto(Guid.NewGuid(), "DIRECTOR", "Director", null, Guid.Parse("70000000-0000-0000-0000-000000000001")));

    /// <summary>
    /// Identificadores enviados a <see cref="ReactivateAsync"/>.
    /// </summary>
    public List<Guid> ReactivateCalls { get; } = new();

    /// <summary>
    /// Excepción opcional que <see cref="ReactivateAsync"/> debe lanzar.
    /// </summary>
    public Exception? ReactivateException { get; set; }

    /// <summary>
    /// Construye un fake que devuelve la lista especificada en
    /// <see cref="GetAllAsync"/>.
    /// </summary>
    public static FakeCargoApiClient WithCargoList(params CargoDto[] cargos)
        => new(cargos, null);

    /// <summary>
    /// Construye un fake que arroja la excepción indicada en
    /// <see cref="GetAllAsync"/>.
    /// </summary>
    public static FakeCargoApiClient WithFailure(Exception exception)
        => new(null, exception);

    public Task<IReadOnlyList<CargoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls.Add(1);

        if (_getAllException is not null)
        {
            throw _getAllException;
        }

        // Las listas en memoria se devuelven filtradas según los ids
        // eliminados durante el test para reflejar el comportamiento real
        // de la API (baja lógica = el cargo ya no aparece como activo).
        IReadOnlyList<CargoDto> snapshot = _getAllResult ?? Array.Empty<CargoDto>();
        if (_deletedIds.Count > 0)
        {
            snapshot = snapshot.Where(c => !_deletedIds.Contains(c.Id)).ToArray();
        }

        return Task.FromResult(snapshot);
    }

    public Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_getAllResult is null)
            return Task.FromResult<CargoDto?>(null);

        if (_deletedIds.Contains(id))
            return Task.FromResult<CargoDto?>(null);

        var cargo = _getAllResult.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(cargo);
    }

    public Task<CargoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);

        if (DeleteResult.Succeeded)
        {
            _deletedIds.Add(id);
        }

        return Task.FromResult(DeleteResult);
    }

    /// <summary>
    /// Indica si el identificador fue marcado como eliminado en este fake
    /// (vía <see cref="DeleteAsync"/>). Útil para tests que necesitan
    /// sembrar bajas lógicas sin tener que invocar el handler HTTP.
    /// </summary>
    public bool IsDeleted(Guid id) => _deletedIds.Contains(id);

    public Task<CargoCommandResult> CreateAsync(CrearCargoRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return Task.FromResult(CreateResult);
    }

    public Task<CargoCommandResult> UpdateAsync(Guid id, ActualizarCargoRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((id, request));

        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<IReadOnlyList<NivelCargoDto>> GetNivelesAsync(CancellationToken cancellationToken = default)
    {
        NivelesCalls++;

        if (NivelesException is not null)
        {
            throw NivelesException;
        }

        return Task.FromResult(NivelesResult);
    }

    public Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken cancellationToken = default)
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

        // El fake ahora modela la semántica server-side del repositorio:
        // sort + filter + paginación coherentes, así los tests web pueden
        // triangular REQ-CM-01 sin pedir a cada test su propio QueryHandler.
        var source = (_getAllResult ?? Array.Empty<CargoDto>()).ToList();
        var snapshot = ApplyStatusFilter(source, query.Status);

        var lowered = query.Search?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(lowered))
        {
            snapshot = snapshot
                .Where(c => c.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || c.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        snapshot = ApplySort(snapshot, query.Sort);

        var total = snapshot.Count;
        var pageItems = snapshot
            .Skip(Math.Max(0, (query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToList();

        return Task.FromResult(new PagedResult<CargoDto>(pageItems, total, query.Page, query.PageSize));
    }

    private List<CargoDto> ApplyStatusFilter(List<CargoDto> source, string? status)
    {
        // Status = "eliminadas" (case-insensitive) → sólo registros en _deletedIds.
        // Status = "activas" (case-insensitive) o null → snapshot activo, idéntico
        // a GetAllAsync (excluye _deletedIds).
        if (string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase))
        {
            return source.Where(c => _deletedIds.Contains(c.Id)).ToList();
        }

        return source.Where(c => !_deletedIds.Contains(c.Id)).ToList();
    }

    private static List<CargoDto> ApplySort(List<CargoDto> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => source.OrderByDescending(static c => c.Codigo, StringComparer.OrdinalIgnoreCase).ToList(),
            "codigo_asc" => source.OrderBy(static c => c.Codigo, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombre_desc" => source.OrderByDescending(static c => c.Nombre, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombre_asc" => source.OrderBy(static c => c.Nombre, StringComparer.OrdinalIgnoreCase).ToList(),
            "nivel_desc" => source.OrderByDescending(static c => c.NivelNombre ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "nivel_asc" => source.OrderBy(static c => c.NivelNombre ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => source.OrderBy(static c => c.Codigo, StringComparer.OrdinalIgnoreCase).ToList()
        };

    public Task<CargoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCalls.Add(id);

        if (ReactivateException is not null)
        {
            throw ReactivateException;
        }

        return Task.FromResult(ReactivateResult);
    }

    // ──────────────────────────────────────────────
    // PR3a — subrecurso CargoSkill (T3.3)
    //
    // Stubs por defecto durante el ciclo RED del strict TDD; T3.3 los
    // consolida con defaults sensatos (lista vacía para Get, Success para
    // Upsert/Delete) y los cohorts (calls) se anexan a continuación.
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resultado fijo que devolverá <see cref="GetSkillsAsync"/>. Por defecto,
    /// lista vacía (la grilla editable parte del estado vacío).
    /// </summary>
    public IReadOnlyList<CargoSkillDetailDto> GetSkillsResult { get; set; } = Array.Empty<CargoSkillDetailDto>();

    /// <summary>
    /// Excepción opcional que <see cref="GetSkillsAsync"/> debe lanzar antes de
    /// devolver el resultado. Útil para tests que simulan caídas de transporte
    /// del subrecurso durante la carga inicial de la página.
    /// </summary>
    public Exception? GetSkillsException { get; set; }

    /// <summary>
    /// Identificadores del cargo consultados vía <see cref="GetSkillsAsync"/>
    /// (incluye el identificador del cargo y la cantidad de invocaciones).
    /// </summary>
    public List<Guid> GetSkillsCalls { get; } = new();

    /// <summary>
    /// Resultado fijo que devolverá <see cref="UpsertSkillAsync"/>. Por defecto,
    /// un Failure de Validation con código <c>FakeNotConfigured</c> para que
    /// cualquier test que olvide cablear explícitamente el resultado falle
    /// de forma ruidosa en vez de devolver silenciosamente
    /// <c>Success(Guid.Empty, Guid.Empty)</c> (default anterior que creaba
    /// la ilusión de cobertura). Los tests que sí quieren un Success lo
    /// reconfiguran explícitamente vía setter.
    /// </summary>
    public CargoSkillCommandResult SkillUpsertResult { get; set; } = CargoSkillCommandResult.Failure(
        new CargoSkillError(
            CargoSkillErrorType.Validation,
            "FakeNotConfigured",
            "SkillUpsertResult no fue cableado en el fake."));

    /// <summary>
    /// Solicitudes recibidas por <see cref="UpsertSkillAsync"/>. Permite
    /// inspeccionar el <c>cargoId</c>, <c>skillId</c> y el payload enviado por
    /// la página a la API en cada test.
    /// </summary>
    public List<(Guid CargoId, Guid SkillId, AsignarCargoSkillRequest Request)> SkillUpsertCalls { get; } = new();

    /// <summary>
    /// Excepción opcional que <see cref="UpsertSkillAsync"/> debe lanzar antes de
    /// devolver el resultado. Útil para tests que verifican el manejo de errores
    /// recuperables en el PageModel.
    /// </summary>
    public Exception? SkillUpsertException { get; set; }

    /// <summary>
    /// Resultado fijo que devolverá <see cref="DeleteSkillAsync"/>. Por defecto,
    /// éxito con <c>204 No Content</c>.
    /// </summary>
    public CargoSkillDeleteResult SkillDeleteResult { get; set; } = new(true, HttpStatusCode.NoContent, null, null);

    /// <summary>
    /// Pares <c>(cargoId, skillId)</c> enviados a <see cref="DeleteSkillAsync"/>.
    /// </summary>
    public List<(Guid CargoId, Guid SkillId)> SkillDeleteCalls { get; } = new();

    /// <summary>
    /// Excepción opcional que <see cref="DeleteSkillAsync"/> debe lanzar antes de
    /// devolver el resultado.
    /// </summary>
    public Exception? SkillDeleteException { get; set; }

    public Task<IReadOnlyList<CargoSkillDetailDto>> GetSkillsAsync(Guid cargoId, CancellationToken cancellationToken = default)
    {
        GetSkillsCalls.Add(cargoId);

        if (GetSkillsException is not null)
        {
            throw GetSkillsException;
        }

        return Task.FromResult(GetSkillsResult);
    }

    public Task<CargoSkillCommandResult> UpsertSkillAsync(Guid cargoId, Guid skillId, AsignarCargoSkillRequest request, CancellationToken cancellationToken = default)
    {
        SkillUpsertCalls.Add((cargoId, skillId, request));

        if (SkillUpsertException is not null)
        {
            throw SkillUpsertException;
        }

        return Task.FromResult(SkillUpsertResult);
    }

    public Task<CargoSkillDeleteResult> DeleteSkillAsync(Guid cargoId, Guid skillId, CancellationToken cancellationToken = default)
    {
        SkillDeleteCalls.Add((cargoId, skillId));

        if (SkillDeleteException is not null)
        {
            throw SkillDeleteException;
        }

        return Task.FromResult(SkillDeleteResult);
    }
}
