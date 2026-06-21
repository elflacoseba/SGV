using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Seguridad.Usuarios;

namespace SGV.Infraestructura.Seguridad;

public sealed class UsuarioIdentityGateway(
    UserManager<SgvIdentityUser> userManager) : IUsuarioIdentityGateway, IUsuarioServicioConsulta
{
    public async Task<UsuarioCommandResult> CrearAsync(
        CrearUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingPersonaUser = await userManager.Users
            .AnyAsync(user => user.PersonaId == request.PersonaId, cancellationToken)
            .ConfigureAwait(false);
        if (existingPersonaUser)
        {
            return UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.Conflict,
                "PersonaYaTieneUsuario",
                "La persona ya tiene un usuario asociado."));
        }

        var user = new SgvIdentityUser
        {
            UserName = request.UserName,
            Email = request.Email,
            PersonaId = request.PersonaId
        };

        var createResult = await userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            return ToValidationResult(createResult);
        }

        var roleResult = await userManager.AddToRolesAsync(user, request.Roles).ConfigureAwait(false);
        if (!roleResult.Succeeded)
        {
            return ToValidationResult(roleResult);
        }

        return UsuarioCommandResult.Success(await MapAsync(user).ConfigureAwait(false));
    }

    public async Task<UsuarioCommandResult> AsignarRolesAsync(
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.NotFound,
                "UsuarioNoEncontrado",
                "El usuario no existe."));
        }

        var currentRoles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles).ConfigureAwait(false);
        if (!removeResult.Succeeded)
        {
            return ToValidationResult(removeResult);
        }

        var addResult = await userManager.AddToRolesAsync(user, roles).ConfigureAwait(false);
        if (!addResult.Succeeded)
        {
            return ToValidationResult(addResult);
        }

        return UsuarioCommandResult.Success(await MapAsync(user).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users
            .OrderBy(user => user.UserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<UsuarioDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(await MapAsync(user).ConfigureAwait(false));
        }

        return result;
    }

    private async Task<UsuarioDto> MapAsync(SgvIdentityUser user)
    {
        var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        return new UsuarioDto(user.Id, user.PersonaId, user.UserName ?? string.Empty, user.Email ?? string.Empty, roles.ToArray());
    }

    private static UsuarioCommandResult ToValidationResult(IdentityResult result)
        => UsuarioCommandResult.Failure(new UsuarioError(
            UsuarioErrorType.Validation,
            "IdentityError",
            string.Join(" ", result.Errors.Select(error => error.Description))));
}
