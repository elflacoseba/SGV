using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Fake en memoria de <see cref="SGV.Web.Integration.Usuarios.IUsuarioApiClient"/>
/// usado por la suite web del módulo Usuarios. Espejo del
/// <c>FakePersonaApiClient</c>: modela el segmento
/// (<c>activas|bloqueadas</c>) y la paginación server-side para que los
/// tests puedan triangular el contrato HTTP sin requerir un backend.
/// </summary>
/// <remarks>
/// <para>
/// Phase 3 del change <c>2026-07-15-quita-soft-delete-usuario</c>: el
/// ciclo de baja lógica (Desactivar/Reactivar) se reemplazó por el
/// ciclo de lockout nativo de Identity (Bloquear/Desbloquear/Eliminar).
/// El fake modela <c>_deletedIds</c> como el conjunto de cuentas
/// borradas físicamente (fueron invocadas a <see cref="EliminarAsync"/>);
/// el segmento <see cref="UsuarioSegmentoListado.Bloqueadas"/> lo modela
/// <c>_lockedIds</c>. El <see cref="QueryAsync"/> filtra por
/// lockout state siguiendo el contrato backend D5.
/// </para>
/// </remarks>
public sealed class FakeUsuarioApiClient : SGV.Web.Integration.Usuarios.IUsuarioApiClient
{
    private readonly IReadOnlyList<UsuarioDto>? _allResult;
    private readonly Exception? _allException;
    private readonly HashSet<string> _deletedIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _lockedIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Construye un fake vacío. Útil para tests del seam (e.g.
    /// verificando que la registración de DI se sobreescribe vía
    /// <see cref="SgvWebApplicationFactory.WithOverrides"/>).
    /// </summary>
    public FakeUsuarioApiClient()
        : this(Array.Empty<UsuarioDto>(), null)
    {
    }

    private FakeUsuarioApiClient(IReadOnlyList<UsuarioDto>? allResult, Exception? allException)
    {
        _allResult = allResult;
        _allException = allException;
    }

    /// <summary>Identificadores enviados a <see cref="EliminarAsync"/>.</summary>
    public List<string> EliminarCalls { get; } = new();

    /// <summary>Identificadores enviados a <see cref="BloquearAsync"/>.</summary>
    public List<string> BloquearCalls { get; } = new();

    /// <summary>Identificadores enviados a <see cref="DesbloquearAsync"/>.</summary>
    public List<string> DesbloquearCalls { get; } = new();

    /// <summary>
    /// Resultado fijo que devuelve <see cref="CreateAsync"/>. Por defecto,
    /// éxito con un DTO genérico.
    /// </summary>
    public UsuarioCommandResult CreateResult { get; set; } = UsuarioCommandResult.Success(
        new UsuarioDto(
            Id: "u-default",
            PersonaId: Guid.NewGuid(),
            UserName: "default",
            Email: "default@example.com",
            Roles: new[] { "Consultor" }));

    /// <summary>Solicitudes recibidas por <see cref="CreateAsync"/>.</summary>
    public List<CrearUsuarioRequest> CreateCalls { get; } = new();

    /// <summary>Excepción opcional que <see cref="CreateAsync"/> lanza.</summary>
    public Exception? CreateException { get; set; }

    /// <summary>
    /// Resultado fijo que devuelve <see cref="UpdateAsync"/>. Por defecto,
    /// éxito con un DTO genérico.
    /// </summary>
    public UsuarioCommandResult UpdateResult { get; set; } = UsuarioCommandResult.Success(
        new UsuarioDto(
            Id: "u-default",
            PersonaId: Guid.NewGuid(),
            UserName: "default-updated",
            Email: "default@example.com",
            Roles: new[] { "Consultor" }));

    /// <summary>Solicitudes recibidas por <see cref="UpdateAsync"/>.</summary>
    public List<(string Id, ActualizarUsuarioRequest Request)> UpdateCalls { get; } = new();

    /// <summary>Excepción opcional que <see cref="UpdateAsync"/> lanza.</summary>
    public Exception? UpdateException { get; set; }

    /// <summary>
    /// Resultado fijo que devuelve <see cref="EliminarAsync"/>. Por
    /// defecto, éxito con <c>Value</c> nulo (alineado con el 204
    /// del backend). Tras éxito se quita el id del universo en memoria
    /// para que <see cref="GetByIdAsync"/> y <see cref="QueryAsync"/>
    /// reflejen el hard-delete.
    /// </summary>
    public UsuarioCommandResult EliminarResult { get; set; } = UsuarioCommandResult.Success(null!);

    /// <summary>Excepción opcional que <see cref="EliminarAsync"/> lanza.</summary>
    public Exception? EliminarException { get; set; }

    /// <summary>
    /// Resultado fijo que devuelve <see cref="BloquearAsync"/>. Por
    /// defecto, éxito con un DTO marcado <c>Bloqueado=true</c>.
    /// </summary>
    public UsuarioCommandResult BloquearResult { get; set; } = UsuarioCommandResult.Success(
        new UsuarioDto(
            Id: "u-default",
            PersonaId: Guid.NewGuid(),
            UserName: "default",
            Email: "default@example.com",
            Roles: new[] { "Consultor" },
            Bloqueado: true));

    /// <summary>Excepción opcional que <see cref="BloquearAsync"/> lanza.</summary>
    public Exception? BloquearException { get; set; }

    /// <summary>
    /// Resultado fijo que devuelve <see cref="DesbloquearAsync"/>. Por
    /// defecto, éxito con un DTO marcado <c>Bloqueado=false</c>.
    /// </summary>
    public UsuarioCommandResult DesbloquearResult { get; set; } = UsuarioCommandResult.Success(
        new UsuarioDto(
            Id: "u-default",
            PersonaId: Guid.NewGuid(),
            UserName: "default",
            Email: "default@example.com",
            Roles: new[] { "Consultor" },
            Bloqueado: false));

    /// <summary>Excepción opcional que <see cref="DesbloquearAsync"/> lanza.</summary>
    public Exception? DesbloquearException { get; set; }

    /// <summary>
    /// Resultado paginado que devuelve <see cref="QueryAsync"/>. Por
    /// defecto se calcula sobre <c>_allResult</c> aplicando el
    /// segmento (<c>activas</c> por defecto, <c>bloqueadas</c> cuando
    /// <c>query.Segmento == UsuarioSegmentoListado.Bloqueadas</c>) y
    /// la paginación.
    /// </summary>
    /// <remarks>
    /// PR2-HALL: el shape wire del PR1 usa wrapper
    /// <c>UsuarioListadoDto(PagedResult&lt;UsuarioDto&gt;)</c>; el
    /// handler devuelve ese wrapper para mantener simetría con el
    /// contrato.
    /// </remarks>
    public Func<UsuarioListQuery, UsuarioListadoDto>? QueryHandler { get; set; }

    /// <summary>Solicitudes recibidas por <see cref="QueryAsync"/>.</summary>
    public List<UsuarioListQuery> QueryCalls { get; } = new();

    /// <summary>Excepción opcional que <see cref="QueryAsync"/> lanza.</summary>
    public Exception? QueryException { get; set; }

    /// <summary>
    /// Construye un fake que devuelve la lista especificada en
    /// <see cref="GetAllActivasAsync"/>.
    /// </summary>
    public static FakeUsuarioApiClient WithUsuarioList(params UsuarioDto[] usuarios)
        => new(usuarios, null);

    /// <summary>
    /// Construye un fake que arroja la excepción indicada en
    /// <see cref="GetAllActivasAsync"/>.
    /// </summary>
    public static FakeUsuarioApiClient WithFailure(Exception exception)
        => new(null, exception);

    /// <summary>
    /// Indica si el identificador fue marcado como borrado físicamente
    /// en este fake (vía <see cref="EliminarAsync"/>). Útil para tests
    /// que necesitan sembrar bajas físicas sin tener que invocar el
    /// handler HTTP.
    /// </summary>
    public bool IsDeleted(string id) => _deletedIds.Contains(id);

    /// <summary>
    /// Indica si el identificador fue marcado como bloqueado en este
    /// fake (vía <see cref="BloquearAsync"/>). Útil para tests que
    /// necesitan sembrar lockouts sin tener que invocar el handler.
    /// </summary>
    public bool IsBlocked(string id) => _lockedIds.Contains(id);

    /// <summary>
    /// Sembrador: marca el id como bloqueado para que las Pages lo
    /// vean en el segmento <see cref="UsuarioSegmentoListado.Bloqueadas"/>
    /// sin pasar por el handler. Útil en tests de auto-fence.
    /// </summary>
    public void SeedBlocked(string id) => _lockedIds.Add(id);

    public Task<IReadOnlyList<UsuarioDto>> GetAllActivasAsync(CancellationToken cancellationToken = default)
    {
        if (_allException is not null)
        {
            throw _allException;
        }

        IReadOnlyList<UsuarioDto> snapshot = _allResult ?? Array.Empty<UsuarioDto>();
        if (_deletedIds.Count > 0 || _lockedIds.Count > 0)
        {
            snapshot = snapshot
                .Where(u => !_deletedIds.Contains(u.Id) && !_lockedIds.Contains(u.Id))
                .ToArray();
        }

        return Task.FromResult(snapshot);
    }

    public Task<UsuarioDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_allResult is null)
        {
            return Task.FromResult<UsuarioDto?>(null);
        }

        if (_deletedIds.Contains(id))
        {
            return Task.FromResult<UsuarioDto?>(null);
        }

        var usuario = _allResult.FirstOrDefault(u => u.Id == id);
        if (usuario is null)
        {
            return Task.FromResult<UsuarioDto?>(null);
        }

        // Phase 3: reflejar el lockout state sobre el DTO que sale del
        // fake para que las Pages observen Bloqueado=true sin necesidad
        // de inyectar DTOs diferentes.
        var bloqueado = _lockedIds.Contains(id);
        if (bloqueado && !usuario.Bloqueado)
        {
            return Task.FromResult<UsuarioDto?>(usuario with { Bloqueado = true });
        }

        return Task.FromResult<UsuarioDto?>(usuario);
    }

    public Task<UsuarioCommandResult> CreateAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        var dto = CreateResult.Value;
        if (dto is not null && CreateResult.IsSuccess)
        {
            var rebased = dto with { Id = $"u-{Guid.NewGuid():N}", PersonaId = request.PersonaId, UserName = request.UserName, Email = request.Email, Roles = request.Roles };
            return Task.FromResult(UsuarioCommandResult.Success(rebased));
        }

        return Task.FromResult(CreateResult);
    }

    public Task<UsuarioCommandResult> UpdateAsync(string id, ActualizarUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((id, request));

        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        var dto = UpdateResult.Value;
        if (dto is not null && UpdateResult.IsSuccess)
        {
            var rebased = dto with { Id = id, UserName = request.UserName, Email = request.Email, Roles = request.Roles };
            return Task.FromResult(UsuarioCommandResult.Success(rebased));
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<UsuarioCommandResult> EliminarAsync(string id, CancellationToken cancellationToken = default)
    {
        EliminarCalls.Add(id);

        if (EliminarException is not null)
        {
            throw EliminarException;
        }

        if (EliminarResult.IsSuccess)
        {
            _deletedIds.Add(id);
            _lockedIds.Remove(id);
        }

        return Task.FromResult(EliminarResult);
    }

    Task<UsuarioCommandResult> SGV.Web.Integration.Usuarios.IUsuarioApiClient.DeleteAsync(
        string id, CancellationToken cancellationToken)
        => EliminarAsync(id, cancellationToken);

    public Task<UsuarioCommandResult> BloquearAsync(string id, CancellationToken cancellationToken = default)
    {
        BloquearCalls.Add(id);

        if (BloquearException is not null)
        {
            throw BloquearException;
        }

        if (BloquearResult.IsSuccess && !_deletedIds.Contains(id))
        {
            _lockedIds.Add(id);
            var dto = BloquearResult.Value;
            if (dto is not null)
            {
                return Task.FromResult(UsuarioCommandResult.Success(dto with { Id = id, Bloqueado = true }));
            }
        }

        return Task.FromResult(BloquearResult);
    }

    public Task<UsuarioCommandResult> DesbloquearAsync(string id, CancellationToken cancellationToken = default)
    {
        DesbloquearCalls.Add(id);

        if (DesbloquearException is not null)
        {
            throw DesbloquearException;
        }

        if (DesbloquearResult.IsSuccess && !_deletedIds.Contains(id))
        {
            _lockedIds.Remove(id);
            var dto = DesbloquearResult.Value;
            if (dto is not null)
            {
                return Task.FromResult(UsuarioCommandResult.Success(dto with { Id = id, Bloqueado = false }));
            }
        }

        return Task.FromResult(DesbloquearResult);
    }

    public Task<UsuarioListadoDto> QueryAsync(UsuarioListQuery query, CancellationToken cancellationToken = default)
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

        var source = (_allResult ?? Array.Empty<UsuarioDto>()).ToList();
        var snapshot = ApplyStatusFilter(source, query.Segmento);

        var lowered = query.Search?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(lowered))
        {
            snapshot = snapshot
                .Where(u => u.UserName.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || u.Email.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || (u.Nombres ?? string.Empty).Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || (u.Apellidos ?? string.Empty).Contains(lowered, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        snapshot = ApplySort(snapshot, query.Sort);

        var total = snapshot.Count;
        var pageItems = snapshot
            .Skip(Math.Max(0, (query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToList();

        return Task.FromResult(new UsuarioListadoDto(
            new PagedResult<UsuarioDto>(
                Items: pageItems,
                TotalCount: total,
                Page: query.Page,
                PageSize: query.PageSize)));
    }

    private List<UsuarioDto> ApplyStatusFilter(List<UsuarioDto> source, UsuarioSegmentoListado segmento)
    {
        return segmento == UsuarioSegmentoListado.Bloqueadas
            ? source
                .Where(u => _lockedIds.Contains(u.Id) && !_deletedIds.Contains(u.Id))
                .Select(u => u with { Bloqueado = true })
                .ToList()
            : source
                .Where(u => !_lockedIds.Contains(u.Id) && !_deletedIds.Contains(u.Id))
                .Select(u => u with { Bloqueado = false })
                .ToList();
    }

    private static List<UsuarioDto> ApplySort(List<UsuarioDto> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "username_desc" or "userName_desc" => source.OrderByDescending(static u => u.UserName, StringComparer.OrdinalIgnoreCase).ToList(),
            "username_asc" or "userName_asc" => source.OrderBy(static u => u.UserName, StringComparer.OrdinalIgnoreCase).ToList(),
            "email_desc" => source.OrderByDescending(static u => u.Email, StringComparer.OrdinalIgnoreCase).ToList(),
            "email_asc" => source.OrderBy(static u => u.Email, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombres_desc" => source.OrderByDescending(static u => u.Nombres ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombres_asc" => source.OrderBy(static u => u.Nombres ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "apellidos_desc" => source.OrderByDescending(static u => u.Apellidos ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "apellidos_asc" => source.OrderBy(static u => u.Apellidos ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => source.OrderBy(static u => u.UserName, StringComparer.OrdinalIgnoreCase).ToList()
        };
}
