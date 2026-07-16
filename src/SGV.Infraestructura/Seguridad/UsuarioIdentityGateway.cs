using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Persistencia;

namespace SGV.Infraestructura.Seguridad;

public sealed class UsuarioIdentityGateway(
    UserManager<SgvIdentityUser> userManager,
    SgvDbContext context) : IUsuarioIdentityGateway, IUsuarioServicioConsulta
{
    public async Task<UsuarioCommandResult> CrearAsync(
        CrearUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // PR #148 review: el índice IX_AspNetUsers_PersonaId fue
        // reemplazado por la columna generada ActivePersonaIdUnique
        // (NULL cuando IsDeleted = 1), de modo que la unicidad SQL vive
        // sólo sobre usuarios activos. Aún así la verificación
        // aplicación-nivel DEBE excluir explícitamente soft-deleted
        // para no reusar la fila eliminada como si aún existiera.
        var existingPersonaUser = await context.Users
            .AnyAsync(user => user.PersonaId == request.PersonaId && !user.IsDeleted, cancellationToken)
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
            IsDeleted = false
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

    public async Task<UsuarioCommandResult> DesactivarAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return UserNotFound();
        }

        // PR #148 review: transacción explícita para mantener
        // simetría con CrearAsync/ActualizarAsync. Aunque el Update es
        // atómico por sí solo, MapAsync ejecuta queries post-update;
        // sin la transacción, un fallo en MapAsync dejaría IsDirty en
        // DB sin propagar el UsuarioCommandResult al caller.
        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        user.IsDeleted = true;
        var updateResult = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(updateResult);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return UsuarioCommandResult.Success(await MapAsync(user, cancellationToken).ConfigureAwait(false));
    }

    public async Task<UsuarioCommandResult> ReactivarAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return UserNotFound();
        }

        // PR #148 review: ver nota en DesactivarAsync — misma
        // justificación para la transacción explícita.
        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        user.IsDeleted = false;
        var updateResult = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ToIdentityFailure(updateResult);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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
    /// <remarks>
    /// PR #148 review: la implementación original pasaba
    /// <c>int.MaxValue</c> como <c>PageSize</c>, lo que provoca un
    /// pull completo de <c>AspNetUsers</c> sin filtrar. Para
    /// datasets grandes, los callers deben migrar a
    /// <see cref="QueryAsync"/> con paginación explícita; este atajo
    /// queda acotado a un máximo razonable de 500 filas, suficiente
    /// para catálogos pequeños y dropdowns.
    /// </remarks>
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
        IQueryable<UsuarioQueryRow> users =
            from user in context.Users.AsNoTracking()
            join persona in context.Personas.AsNoTracking()
                on user.PersonaId equals persona.Id
            where query.Segmento == UsuarioSegmentoListado.Activas
                ? !user.IsDeleted
                : user.IsDeleted
            select new UsuarioQueryRow
            {
                Id = user.Id,
                PersonaId = user.PersonaId,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Nombres = persona.Nombres,
                Apellidos = persona.Apellidos
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
                row.Apellidos
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
                group.Key.Apellidos))
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
        return new UsuarioDto(
            user.Id,
            user.PersonaId,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            persona?.Nombres,
            persona?.Apellidos);
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
    }

    private sealed class UsuarioRoleRow : UsuarioQueryRow
    {
        public string? Role { get; init; }
    }
}
