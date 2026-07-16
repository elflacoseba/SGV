using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;

namespace SGV.Infraestructura.Seguridad;

/// <summary>
/// Identity-backed implementation of <see cref="IUsuarioIdentityGateway"/>
/// and <see cref="IUsuarioServicioConsulta"/>.
/// </summary>
/// <remarks>
/// Cambio <c>2026-07-15-quita-soft-delete-usuario</c>: la columna
/// <c>IsDeleted</c> y las columnas generadas soft-delete-aware se
/// retiran. La separación activa/bloqueada se hace ahora vía
/// <c>LockoutEnd &gt; UtcNow</c> en <see cref="QueryAsync"/>. Las
/// operaciones de ciclo de vida (<c>Bloquear</c>, <c>Desbloquear</c>,
/// <c>Eliminar</c>) reemplazan a <c>Desactivar</c>/<c>Reactivar</c>.
/// </remarks>
public sealed class UsuarioIdentityGateway(
    UserManager<SgvIdentityUser> userManager,
    SgvDbContext context) : IUsuarioIdentityGateway, IUsuarioServicioConsulta
{
    /// <summary>
    /// Sentinel lockout date for administrative block. Matches the
    /// design decision D1: <c>datetime(6)</c> maximum accepted by MySQL
    /// is <c>9999-12-31 23:59:59.999999</c>. We store it as
    /// <c>9999-12-31 23:59:59</c> at second precision; the column type
    /// (<c>datetime(6)</c>) preserves it as <c>9999-12-31 23:59:59.000000</c>.
    /// <see cref="DateTimeOffset.MaxValue"/> would overflow the 7th
    /// fraction during MySQL round-trip (see Engram #1135).
    /// </summary>
    private static readonly DateTimeOffset LockoutSentinelUtc =
        new(9999, 12, 31, 23, 59, 59, TimeSpan.Zero);

    public async Task<UsuarioCommandResult> CrearAsync(
        CrearUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existingPersonaUser = await context.Users
            .AnyAsync(user => user.PersonaId == request.PersonaId, cancellationToken)
            .ConfigureAwait(false);
        if (existingPersonaUser)
        {
            return Failure(
                UsuarioErrorType.Conflict,
                "PersonaYaTieneUsuario",
                "La persona ya tiene un usuario asociado.",
                ErrorCategoria.Conflict);
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var user = new SgvIdentityUser
        {
            UserName = request.UserName,
            Email = request.Email,
            PersonaId = request.PersonaId,
        };

        var createResult = await userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(createResult);
        }

        var roleResult = await userManager.AddToRolesAsync(user, request.Roles).ConfigureAwait(false);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(roleResult);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return UsuarioCommandResult.Success(await MapAsync(user, cancellationToken).ConfigureAwait(false));
    }

    public async Task<UsuarioCommandResult> AsignarRolesAsync(
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return UserNotFound();
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var currentRoles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        var replaceResult = await ReplaceRolesAsync(user, currentRoles, roles).ConfigureAwait(false);
        if (replaceResult is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return replaceResult;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return UsuarioCommandResult.Success(await MapAsync(user, cancellationToken).ConfigureAwait(false));
    }

    public Task<UsuarioDto?> ObtenerAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => GetByIdAsync(userId, cancellationToken);

    public async Task<UsuarioCommandResult> ActualizarAsync(
        string userId,
        ActualizarUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return UserNotFound();
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var currentRoles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        user.UserName = request.UserName;
        user.Email = request.Email;

        var updateResult = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(updateResult);
        }

        var replaceResult = await ReplaceRolesAsync(user, currentRoles, request.Roles).ConfigureAwait(false);
        if (replaceResult is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return replaceResult;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return UsuarioCommandResult.Success(await MapAsync(user, cancellationToken).ConfigureAwait(false));
    }

    public async Task<UsuarioCommandResult> BloquearAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return UserNotFound();
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // LockoutEnabled must be true so IsLockedOutAsync honours the
        // date. Without this, Identity silently ignores SetLockoutEndDateAsync.
        user.LockoutEnabled = true;
        var lockoutResult = await userManager.SetLockoutEndDateAsync(user, LockoutSentinelUtc).ConfigureAwait(false);
        if (!lockoutResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(lockoutResult);
        }

        var updateResult = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(updateResult);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return UsuarioCommandResult.Success(await MapAsync(user, cancellationToken).ConfigureAwait(false));
    }

    public async Task<UsuarioCommandResult> DesbloquearAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return UserNotFound();
        }

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var unlockResult = await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        if (!unlockResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(unlockResult);
        }

        // LockoutEnabled stays true per Identity contract — only
        // LockoutEnd is cleared. This preserves the user's
        // AccessFailedCount so a subsequent brute-force attempt can
        // re-lock the account without resetting state.
        var updateResult = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(updateResult);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return UsuarioCommandResult.Success(await MapAsync(user, cancellationToken).ConfigureAwait(false));
    }

    public async Task<UsuarioCommandResult> EliminarAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return UserNotFound();
        }

        // DeleteAsync triggers FK CASCADE on AspNetUserRoles/Claims/
        // Logins/Tokens. Persona RESTRICT FK stays intact; Auditorias
        // holds a string column without FK so historical references
        // remain queryable (see design D4).
        var deleteResult = await userManager.DeleteAsync(user).ConfigureAwait(false);
        if (!deleteResult.Succeeded)
        {
            return ToIdentityFailure(deleteResult);
        }

        return UsuarioCommandResult.Success(await MapAsync(user, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Atajo preservado para callers que necesitan el catálogo plano
    /// de usuarios activos (e.g. dropdowns administrativos). Mantiene
    /// la firma <see cref="IReadOnlyList{UsuarioDto}"/> sin
    /// paginación para no romper los call sites vigentes, pero ACOTA
    /// el <c>PageSize</c> a <see cref="MaxListPageSize"/> para evitar
    /// materializar potencialmente miles de filas en memoria.
    /// </summary>
    public async Task<IReadOnlyList<UsuarioDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(
            new UsuarioListQuery(1, MaxListPageSize, null, "username_asc"),
            cancellationToken).ConfigureAwait(false);
        return result.Items;
    }

    private const int MaxListPageSize = 500;

    public async Task<UsuarioDto?> GetByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        return user is null ? null : await MapAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<UsuarioDto>> QueryAsync(
        UsuarioListQuery query,
        CancellationToken cancellationToken = default)
    {
        // Activos: LockoutEnd null o en el pasado.
        // Bloqueadas: LockoutEnd futuro (vigente). Cualquier LockoutEnd
        // futuro cuenta, sin importar si es administrativo o por
        // MaxFailedAccessAttempts — ver D5.
        var nowUtc = DateTimeOffset.UtcNow;
        IQueryable<UsuarioQueryRow> users =
            from user in context.Users.AsNoTracking()
            join persona in context.Personas.AsNoTracking()
                on user.PersonaId equals persona.Id
            where query.Segmento == UsuarioSegmentoListado.Activas
                ? (user.LockoutEnd == null || user.LockoutEnd <= nowUtc)
                : user.LockoutEnd > nowUtc
            select new UsuarioQueryRow
            {
                Id = user.Id,
                PersonaId = user.PersonaId,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Nombres = persona.Nombres,
                Apellidos = persona.Apellidos,
                LockoutEnd = user.LockoutEnd
            };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users.Where(user =>
                user.UserName.Contains(search)
                || user.Email.Contains(search)
                || user.Nombres.Contains(search)
                || user.Apellidos.Contains(search));
        }

        var totalCount = await users.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = ApplySort(users, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);

        var rows = await (
            from user in page
            join userRole in context.UserRoles.AsNoTracking()
                on user.Id equals userRole.UserId into userRoles
            from userRole in userRoles.DefaultIfEmpty()
            join role in context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new UsuarioRoleRow
            {
                Id = user.Id,
                PersonaId = user.PersonaId,
                UserName = user.UserName,
                Email = user.Email,
                Nombres = user.Nombres,
                Apellidos = user.Apellidos,
                LockoutEnd = user.LockoutEnd,
                Role = role == null ? null : role.Name
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .GroupBy(row => new
            {
                row.Id,
                row.PersonaId,
                row.UserName,
                row.Email,
                row.Nombres,
                row.Apellidos,
                row.LockoutEnd
            })
            .Select(group => new UsuarioDto(
                group.Key.Id,
                group.Key.PersonaId,
                group.Key.UserName,
                group.Key.Email,
                group.Where(row => row.Role is not null)
                    .Select(row => row.Role!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(role => role, StringComparer.Ordinal)
                    .ToArray(),
                group.Key.Nombres,
                group.Key.Apellidos,
                Bloqueado: group.Key.LockoutEnd is { } end && end > DateTimeOffset.UtcNow))
            .ToArray();

        return new PagedResult<UsuarioDto>(items, totalCount, query.Page, query.PageSize);
    }

    private async Task<UsuarioCommandResult?> ReplaceRolesAsync(
        SgvIdentityUser user,
        IEnumerable<string> currentRoles,
        IReadOnlyCollection<string> requestedRoles)
    {
        var rolesToRemove = currentRoles.Except(requestedRoles, StringComparer.Ordinal).ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove).ConfigureAwait(false);
            if (!removeResult.Succeeded)
            {
                return ToIdentityFailure(removeResult);
            }
        }

        var rolesToAdd = requestedRoles.Except(currentRoles, StringComparer.Ordinal).ToArray();
        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd).ConfigureAwait(false);
            if (!addResult.Succeeded)
            {
                return ToIdentityFailure(addResult);
            }
        }

        return null;
    }

    private async Task<UsuarioDto> MapAsync(
        SgvIdentityUser user,
        CancellationToken cancellationToken)
    {
        var persona = await context.Personas
            .AsNoTracking()
            .Where(item => item.Id == user.PersonaId)
            .Select(item => new { item.Nombres, item.Apellidos })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        // Rea-009 / RIS-006: lockout flag derived from LockoutEnd. Identity's
        // contract is LockoutEnd > UtcNow ⇒ user is locked out.
        var bloqueado = user.LockoutEnd is { } lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow;
        return new UsuarioDto(
            user.Id,
            user.PersonaId,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            persona?.Nombres,
            persona?.Apellidos,
            Bloqueado: bloqueado);
    }

    private static IOrderedQueryable<UsuarioQueryRow> ApplySort(
        IQueryable<UsuarioQueryRow> query,
        string? sort)
        => sort?.ToLowerInvariant() switch
        {
            "username_desc" => query.OrderByDescending(user => user.UserName),
            "email_asc" => query.OrderBy(user => user.Email).ThenBy(user => user.UserName),
            "email_desc" => query.OrderByDescending(user => user.Email).ThenBy(user => user.UserName),
            "nombres_asc" => query.OrderBy(user => user.Nombres).ThenBy(user => user.Apellidos),
            "nombres_desc" => query.OrderByDescending(user => user.Nombres).ThenByDescending(user => user.Apellidos),
            "apellidos_asc" => query.OrderBy(user => user.Apellidos).ThenBy(user => user.Nombres),
            "apellidos_desc" => query.OrderByDescending(user => user.Apellidos).ThenByDescending(user => user.Nombres),
            _ => query.OrderBy(user => user.UserName)
        };

    private static UsuarioCommandResult ToIdentityFailure(IdentityResult result)
    {
        var errors = result.Errors.ToArray();
        if (errors.Any(error => string.Equals(error.Code, "DuplicateUserName", StringComparison.Ordinal)))
        {
            return Failure(
                UsuarioErrorType.Conflict,
                "UserNameDuplicado",
                "El nombre de usuario ya está en uso.",
                ErrorCategoria.Conflict);
        }

        if (errors.Any(error => string.Equals(error.Code, "DuplicateEmail", StringComparison.Ordinal)))
        {
            return Failure(
                UsuarioErrorType.Conflict,
                "EmailDuplicado",
                "El email ya está en uso.",
                ErrorCategoria.Conflict);
        }

        return Failure(
            UsuarioErrorType.Validation,
            "IdentityError",
            string.Join(" ", errors.Select(error => error.Description)),
            ErrorCategoria.Validation);
    }

    private static UsuarioCommandResult UserNotFound()
        => Failure(
            UsuarioErrorType.NotFound,
            "UsuarioNoEncontrado",
            "El usuario no existe.",
            ErrorCategoria.NotFound);

    private static UsuarioCommandResult Failure(
        UsuarioErrorType type,
        string code,
        string message,
        ErrorCategoria categoria)
        => UsuarioCommandResult.Failure(new UsuarioError(type, code, message, Categoria: categoria));

    private class UsuarioQueryRow
    {
        public required string Id { get; init; }
        public Guid PersonaId { get; init; }
        public required string UserName { get; init; }
        public required string Email { get; init; }
        public required string Nombres { get; init; }
        public required string Apellidos { get; init; }
        // Rea-009 / RIS-006: required to populate UsuarioDto.Bloqueado in
        // QueryAsync without an extra roundtrip to AspNetUsers.
        public DateTimeOffset? LockoutEnd { get; init; }
    }

    private sealed class UsuarioRoleRow : UsuarioQueryRow
    {
        public string? Role { get; init; }
    }
}