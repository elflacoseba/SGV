using System.Net;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Fake en memoria de <see cref="SGV.Web.Integration.Personas.IPersonaApiClient"/>
/// usado por la suite web del módulo Personas (PR 4/4 del change
/// <c>2026-07-14-frontend-crud-personas</c>). Espejo del
/// <c>FakeCargoApiClient</c>: modela el segmento
/// (<c>activas|eliminadas</c>) y la paginación server-side para que los
/// tests web puedan triangular REQ-CM-01 sin pedir a cada test su propio
/// <c>QueryHandler</c>.
/// </summary>
public sealed class FakePersonaApiClient : SGV.Web.Integration.Personas.IPersonaApiClient
{
    private readonly IReadOnlyList<PersonaDto>? _getAllResult;
    private readonly Exception? _getAllException;
    private readonly HashSet<Guid> _deletedIds = new();

    /// <summary>
    /// Construye un fake vacío. Útil para tests del seam que sólo necesitan
    /// confirmar el override del servicio en el contenedor.
    /// </summary>
    public FakePersonaApiClient()
        : this(Array.Empty<PersonaDto>(), null)
    {
    }

    private FakePersonaApiClient(IReadOnlyList<PersonaDto>? getAllResult, Exception? getAllException)
    {
        _getAllResult = getAllResult;
        _getAllException = getAllException;
    }

    /// <summary>Cantidad de invocaciones a <see cref="GetAllAsync"/>.</summary>
    public List<int> GetAllCalls { get; } = new();

    /// <summary>Identificadores enviados a <see cref="DesactivarAsync"/>.</summary>
    public List<Guid> DeleteCalls { get; } = new();

    /// <summary>
    /// Resultado fijo que devuelve <see cref="DesactivarAsync"/>. Por
    /// defecto, éxito con <c>204 No Content</c>.
    /// </summary>
    public PersonaDeleteResult DeleteResult { get; set; } = new(true, HttpStatusCode.NoContent, null, null);

    /// <summary>
    /// Resultado fijo que devuelve <see cref="CreateAsync"/>. Por defecto,
    /// <c>NotImplemented</c> para forzar a los tests a configurarlo
    /// explícitamente cuando lo necesiten.
    /// </summary>
    public PersonaCommandResult CreateResult { get; set; } = PersonaCommandResult.Failure(
        new PersonaError(PersonaErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    /// <summary>Solicitudes recibidas por <see cref="CreateAsync"/>.</summary>
    public List<CrearPersonaRequest> CreateCalls { get; } = new();

    /// <summary>Excepción opcional que <see cref="CreateAsync"/> lanza.</summary>
    public Exception? CreateException { get; set; }

    /// <summary>
    /// Resultado fijo que devuelve <see cref="UpdateAsync"/>. Por defecto,
    /// <c>NotImplemented</c> para forzar a los tests a configurarlo
    /// explícitamente cuando lo necesiten.
    /// </summary>
    public PersonaCommandResult UpdateResult { get; set; } = PersonaCommandResult.Failure(
        new PersonaError(PersonaErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    /// <summary>Solicitudes recibidas por <see cref="UpdateAsync"/>.</summary>
    public List<(Guid Id, ActualizarPersonaRequest Request)> UpdateCalls { get; } = new();

    /// <summary>Excepción opcional que <see cref="UpdateAsync"/> lanza.</summary>
    public Exception? UpdateException { get; set; }

    /// <summary>
    /// Resultado paginado que devuelve <see cref="QueryAsync"/>. Por defecto
    /// se calcula sobre <c>_getAllResult</c> aplicando el segmento
    /// (<c>activas</c> por defecto, <c>eliminadas</c> cuando
    /// <c>query.Segmento == PersonaSegmentoListado.Eliminadas</c>) y la
    /// paginación.
    /// </summary>
    public Func<SGV.Contracts.Personas.Consultas.Dtos.PersonaListQuery, PersonaListadoDto>? QueryHandler { get; set; }

    /// <summary>Solicitudes recibidas por <see cref="QueryAsync"/>.</summary>
    public List<SGV.Contracts.Personas.Consultas.Dtos.PersonaListQuery> QueryCalls { get; } = new();

    /// <summary>Excepción opcional que <see cref="QueryAsync"/> lanza.</summary>
    public Exception? QueryException { get; set; }

    /// <summary>
    /// Resultado fijo que devuelve <see cref="ReactivarAsync"/>. Por defecto,
    /// éxito con el DTO de la primera persona activa.
    /// </summary>
    public PersonaCommandResult ReactivarResult { get; set; } = PersonaCommandResult.Success(
        new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", null, null, null, null, true));

    /// <summary>Identificadores enviados a <see cref="ReactivarAsync"/>.</summary>
    public List<Guid> ReactivarCalls { get; } = new();

    /// <summary>Excepción opcional que <see cref="ReactivarAsync"/> lanza.</summary>
    public Exception? ReactivarException { get; set; }

    /// <summary>
    /// Conjunto de identificadores de personas activas que ya tienen un
    /// usuario asociado. Cuando <see cref="QueryAsync"/> recibe un
    /// <see cref="SGV.Contracts.Personas.Consultas.Dtos.PersonaListQuery"/>
    /// con <c>SoloSinUsuario == true</c>, estos ids se excluyen del
    /// resultado (espejo del anti-join contra
    /// <c>AspNetUsers.PersonaId</c> que hace el repositorio real). Cambio
    /// WU-4 del change <c>2026-07-17-buscador-personas-modal</c>.
    /// </summary>
    private readonly HashSet<Guid> _soloSinUsuarioSet = new();

    /// <summary>
    /// Construye un fake que devuelve la lista especificada en
    /// <see cref="GetAllAsync"/>.
    /// </summary>
    public static FakePersonaApiClient WithPersonaList(params PersonaDto[] personas)
        => new(personas, null);

    /// <summary>
    /// Construye un fake que arroja la excepción indicada en
    /// <see cref="GetAllAsync"/>.
    /// </summary>
    public static FakePersonaApiClient WithFailure(Exception exception)
        => new(null, exception);

    /// <summary>
    /// Registra qué personas activas ya tienen un usuario asociado en el
    /// fake. <see cref="QueryAsync"/> los excluirá del resultado cuando el
    /// query solicite <c>SoloSinUsuario == true</c>. Helper fluido que
    /// permite encadenar configuración:
    /// <c>FakePersonaApiClient.WithPersonaList(...).WithSoloSinUsuarioSet(...)</c>.
    /// </summary>
    public FakePersonaApiClient WithSoloSinUsuarioSet(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (var id in ids)
        {
            _soloSinUsuarioSet.Add(id);
        }
        return this;
    }

    public Task<IReadOnlyList<PersonaDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls.Add(1);

        if (_getAllException is not null)
        {
            throw _getAllException;
        }

        IReadOnlyList<PersonaDto> snapshot = _getAllResult ?? Array.Empty<PersonaDto>();
        if (_deletedIds.Count > 0)
        {
            snapshot = snapshot.Where(p => !_deletedIds.Contains(p.Id)).ToArray();
        }

        return Task.FromResult(snapshot);
    }

    public Task<PersonaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_getAllResult is null)
            return Task.FromResult<PersonaDto?>(null);

        if (_deletedIds.Contains(id))
            return Task.FromResult<PersonaDto?>(null);

        var persona = _getAllResult.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(persona);
    }

    public Task<PersonaDeleteResult> DesactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);

        if (DeleteResult.Succeeded)
        {
            _deletedIds.Add(id);
        }

        return Task.FromResult(DeleteResult);
    }

    Task<PersonaDeleteResult> SGV.Web.Integration.Personas.IPersonaApiClient.DeleteAsync(
        Guid id, CancellationToken cancellationToken)
        => DesactivarAsync(id, cancellationToken);

    /// <summary>
    /// Indica si el identificador fue marcado como eliminado en este fake
    /// (vía <see cref="DesactivarAsync"/>). Útil para tests que necesitan
    /// sembrar bajas lógicas sin tener que invocar el handler HTTP.
    /// </summary>
    public bool IsDeleted(Guid id) => _deletedIds.Contains(id);

    public Task<PersonaCommandResult> CreateAsync(CrearPersonaRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return Task.FromResult(CreateResult);
    }

    public Task<PersonaCommandResult> UpdateAsync(Guid id, ActualizarPersonaRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((id, request));

        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<PersonaListadoDto> QueryAsync(
        SGV.Contracts.Personas.Consultas.Dtos.PersonaListQuery query,
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

        var source = (_getAllResult ?? Array.Empty<PersonaDto>()).ToList();
        var snapshot = ApplyStatusFilter(source, query.Segmento);

        // Filtro `soloSinUsuario`: cuando `true`, excluimos las personas
        // activas que ya tienen un usuario asociado. Cuando es `null` o
        // `false`, el filtro se omite para preservar back-compat con
        // consumidores vigentes (Index Personas, typeahead).
        snapshot = ApplySoloSinUsuarioFilter(snapshot, query.SoloSinUsuario);

        var lowered = query.Search?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(lowered))
        {
            snapshot = snapshot
                .Where(p => (p.Legajo ?? string.Empty).Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || p.Nombres.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || p.Apellidos.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || (p.Email ?? string.Empty).Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || (p.NumeroDocumento ?? string.Empty).Contains(lowered, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        snapshot = ApplySort(snapshot, query.Sort);

        var total = snapshot.Count;
        var pageItems = snapshot
            .Skip(Math.Max(0, (query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToList();

        return Task.FromResult(new PersonaListadoDto(pageItems, total, query.Page, query.PageSize));
    }

    public Task<PersonaCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivarCalls.Add(id);

        if (ReactivarException is not null)
        {
            throw ReactivarException;
        }

        return Task.FromResult(ReactivarResult);
    }

    private List<PersonaDto> ApplyStatusFilter(List<PersonaDto> source, PersonaSegmentoListado segmento)
    {
        // Segmento Eliminadas → sólo ids marcados como eliminados en este fake.
        // Segmento Activas o default → snapshot activo (excluye _deletedIds).
        return segmento == PersonaSegmentoListado.Eliminadas
            ? source.Where(p => _deletedIds.Contains(p.Id)).ToList()
            : source.Where(p => !_deletedIds.Contains(p.Id)).ToList();
    }

    /// <summary>
    /// Aplica el filtro <c>SoloSinUsuario</c>: cuando el query pide
    /// <c>true</c>, excluye los ids registrados vía
    /// <see cref="WithSoloSinUsuarioSet"/>. Cuando es <c>null</c> o
    /// <c>false</c>, no aplica ningún filtro (back-compat). Espejo del
    /// anti-join contra <c>AspNetUsers.PersonaId</c> del repo real
    /// (REQ-PM-01, REQ-USB-10).
    /// </summary>
    private List<PersonaDto> ApplySoloSinUsuarioFilter(List<PersonaDto> source, bool? soloSinUsuario)
    {
        if (soloSinUsuario != true)
        {
            return source;
        }

        return source.Where(p => !_soloSinUsuarioSet.Contains(p.Id)).ToList();
    }

    private static List<PersonaDto> ApplySort(List<PersonaDto> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "legajo_desc" => source.OrderByDescending(static p => p.Legajo ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "legajo_asc" => source.OrderBy(static p => p.Legajo ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "apellidos_desc" => source.OrderByDescending(static p => p.Apellidos, StringComparer.OrdinalIgnoreCase).ToList(),
            "apellidos_asc" => source.OrderBy(static p => p.Apellidos, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombres_desc" => source.OrderByDescending(static p => p.Nombres, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombres_asc" => source.OrderBy(static p => p.Nombres, StringComparer.OrdinalIgnoreCase).ToList(),
            "email_desc" => source.OrderByDescending(static p => p.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "email_asc" => source.OrderBy(static p => p.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => source.OrderBy(static p => p.Apellidos, StringComparer.OrdinalIgnoreCase).ToList()
        };
}