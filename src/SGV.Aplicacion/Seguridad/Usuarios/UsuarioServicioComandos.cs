using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Seguridad;

namespace SGV.Aplicacion.Seguridad.Usuarios;

public sealed class UsuarioServicioComandos(
    IPersonaRepository personaRepository,
    IUsuarioIdentityGateway identityGateway) : IUsuarioServicioComandos
{
    public async Task<UsuarioCommandResult> CrearAsync(
        CrearUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PersonaId == Guid.Empty)
        {
            return Validation("PersonaRequerida", "La persona es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Validation("DatosInvalidos", "Usuario, email y contraseña son obligatorios.");
        }

        if (request.Roles.Count == 0 || !RolesSgv.TodosValidos(request.Roles))
        {
            return Validation("RolNoSoportado", "Uno o más roles no pertenecen al catálogo fijo de SGV.");
        }

        var persona = await personaRepository.GetByIdAsync(request.PersonaId, cancellationToken).ConfigureAwait(false);
        if (persona is null)
        {
            return UsuarioCommandResult.Failure(new UsuarioError(
                UsuarioErrorType.NotFound,
                "PersonaNoEncontrada",
                "La persona asociada al usuario no existe."));
        }

        return await identityGateway.CrearAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UsuarioCommandResult> AsignarRolesAsync(
        string userId,
        AsignarRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("UsuarioRequerido", "El usuario es obligatorio.");
        }

        if (request.Roles.Count == 0 || !RolesSgv.TodosValidos(request.Roles))
        {
            return Validation("RolNoSoportado", "Uno o más roles no pertenecen al catálogo fijo de SGV.");
        }

        return await identityGateway.AsignarRolesAsync(userId, request.Roles, cancellationToken).ConfigureAwait(false);
    }

    private static UsuarioCommandResult Validation(string code, string message)
        => UsuarioCommandResult.Failure(new UsuarioError(UsuarioErrorType.Validation, code, message));
}
