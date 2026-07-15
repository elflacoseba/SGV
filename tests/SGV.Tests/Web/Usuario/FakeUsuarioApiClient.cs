using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Fake en memoria de <see cref="SGV.Web.Integration.Usuarios.IUsuarioApiClient"/>
/// usado por la suite web del módulo Usuarios (PR 2 introduce la
/// forma del fake; las Pages que lo consumen llegan en PR 3/4).
/// Espejo del <c>FakePersonaApiClient</c>: modela el segmento
/// (<c>activas|eliminadas</c>) y la paginación server-side para que los
/// tests puedan triangular el contrato HTTP sin requerir un backend.
/// </summary>
/// <remarks>
/// <para>
/// A diferencia del <c>FakePersonaApiClient</c>, este fake devuelve
/// siempre <see cref="UsuarioCommandResult"/> (no existe un
/// <c>UsuarioDeleteResult</c> dedicado — el shape Delete del backend
/// emite 200 con DTO activo para soportar la rama AutoBaja, así que
/// el <c>CommandResult</c> común cubre éxito y fallo con field errors).
/// </para>
/// </remarks>
public sealed class FakeUsuarioApiClient : SGV.Web.Integration.Usuarios.IUsuarioApiClient
{
    private readonly IReadOnlyList<UsuarioDto>? _allResult;
    private readonly Exception? _allException;
    private readonly HashSet<string> _deletedIds = new(StringComparer.Ordinal);

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

    /// <summary>Identificadores enviados a <see cref="DesactivarAsync"/>.</summary>
    public List<string> DeleteCalls { get; } = new();

    /// <summary>Identificadores enviados a <see cref="ReactivarAsync"/>.</summary>
    public List<string> ReactivarCalls { get; } = new();

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
    /// Resultado fijo que devuelve <see cref="DesactivarAsync"/>. Por
    /// defecto, éxito con un DTO activo (Id se sobrescribe con el
    /// solicitado al momento de responder).
    /// </summary>
    public UsuarioCommandResult DesactivarResult { get; set; } = UsuarioCommandResult.Success(
        new UsuarioDto(
            Id: "u-default",
            PersonaId: Guid.NewGuid(),
            UserName: "default",
            Email: "default@example.com",
            Roles: new[] { "Consultor" }));

    /// <summary>Excepción opcional que <see cref="DesactivarAsync"/> lanza.</summary>
    public Exception? DesactivarException { get; set; }

    /// <summary>
    /// Resultado fijo que devuelve <see cref="ReactivarAsync"/>. Por
    /// defecto, éxito con un DTO activo.
    /// </summary>
    public UsuarioCommandResult ReactivarResult { get; set; } = UsuarioCommandResult.Success(
        new UsuarioDto(
            Id: "u-default",
            PersonaId: Guid.NewGuid(),
            UserName: "default-reactivated",
            Email: "default@example.com",
            Roles: new[] { "Consultor" }));

    /// <summary>Excepción opcional que <see cref="ReactivarAsync"/> lanza.</summary>
    public Exception? ReactivarException { get; set; }

    /// <summary>
    /// Resultado paginado que devuelve <see cref="QueryAsync"/>. Por
    /// defecto se calcula sobre <c>_allResult</c> aplicando el
    /// segmento (<c>activas</c> por defecto, <c>eliminadas</c> cuando
    /// <c>query.Segmento == UsuarioSegmentoListado.Eliminadas</c>) y
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
    /// Indica si el identificador fue marcado como eliminado en este
    /// fake (vía <see cref="DesactivarAsync"/>). Útil para tests que
    /// necesitan sembrar bajas lógicas sin tener que invocar el
    /// handler HTTP.
    /// </summary>
    public bool IsDeleted(string id) => _deletedIds.Contains(id);

    public Task<IReadOnlyList<UsuarioDto>> GetAllActivasAsync(CancellationToken cancellationToken = default)
    {
        if (_allException is not null)
        {
            throw _allException;
        }

        IReadOnlyList<UsuarioDto> snapshot = _allResult ?? Array.Empty<UsuarioDto>();
        if (_deletedIds.Count > 0)
        {
            snapshot = snapshot.Where(u => !_deletedIds.Contains(u.Id)).ToArray();
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
        return Task.FromResult(usuario);
    }

    public Task<UsuarioCommandResult> CreateAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        // Sobrescribir el Id del resultado con un guid para que el
        // test no asuma Id="u-default" si no lo configura.
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

    public Task<UsuarioCommandResult> DesactivarAsync(string id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);

        if (DesactivarException is not null)
        {
            throw DesactivarException;
        }

        if (DesactivarResult.IsSuccess)
        {
            _deletedIds.Add(id);
            var dto = DesactivarResult.Value;
            if (dto is not null)
            {
                return Task.FromResult(UsuarioCommandResult.Success(dto with { Id = id }));
            }
        }

        return Task.FromResult(DesactivarResult);
    }

    Task<UsuarioCommandResult> SGV.Web.Integration.Usuarios.IUsuarioApiClient.DeleteAsync(
        string id, CancellationToken cancellationToken)
        => DesactivarAsync(id, cancellationToken);

    public Task<UsuarioCommandResult> ReactivarAsync(string id, CancellationToken cancellationToken = default)
    {
        ReactivarCalls.Add(id);

        if (ReactivarException is not null)
        {
            throw ReactivarException;
        }

        if (ReactivarResult.IsSuccess)
        {
            _deletedIds.Remove(id);

            var dto = ReactivarResult.Value;
            if (dto is not null)
            {
                return Task.FromResult(UsuarioCommandResult.Success(dto with { Id = id }));
            }
        }

        return Task.FromResult(ReactivarResult);
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

        // PR2-HALL: el shape wire del PR1 usa wrapper
        // `UsuarioListadoDto(PagedResult<UsuarioDto>)`. Mantener este
        // wrapper al construirlo a mano es trivial; lo hacemos
        // explícito para que el gap quede visible.
        return Task.FromResult(new UsuarioListadoDto(
            new PagedResult<UsuarioDto>(
                Items: pageItems,
                TotalCount: total,
                Page: query.Page,
                PageSize: query.PageSize)));
    }

    public Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_allResult is null)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var usuario = _allResult.FirstOrDefault(u => u.Id == userId);
        var roles = usuario?.Roles;
        IReadOnlyList<string> result = roles is null ? Array.Empty<string>() : roles.ToArray();
        return Task.FromResult(result);
    }

    private List<UsuarioDto> ApplyStatusFilter(List<UsuarioDto> source, UsuarioSegmentoListado segmento)
    {
        return segmento == UsuarioSegmentoListado.Eliminadas
            ? source.Where(u => _deletedIds.Contains(u.Id)).ToList()
            : source.Where(u => !_deletedIds.Contains(u.Id)).ToList();
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
