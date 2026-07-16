using System.Net.Mail;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Seguridad;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.Usuarios;

public sealed class UsuarioServicioComandos(
    IPersonaRepository personaRepository,
    IUsuarioIdentityGateway identityGateway,
    IUsuarioActual usuarioActual,
    IAuditoriaServicio auditoriaServicio) : IUsuarioServicioComandos
{
    private const string EntidadAuditada = "Usuario";

    public async Task<UsuarioCommandResult> CrearAsync(
        CrearUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PersonaId == Guid.Empty)
        {
            return Validation("PersonaRequerida", "La persona es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return Validation("DatosInvalidos", "Usuario, email y contraseña son obligatorios.");
        }

        // PR #148 review: ActualizarAsync ya validaba el formato del
        // email con MailAddress.TryCreate. Replicamos el helper
        // compartido aquí para no aceptar emails que el backend de
        // Identity rechazaría downstream con un error menos explícito.
        if (!IsValidEmail(request.Email))
        {
            return Validation("EmailInvalido", "El email no tiene un formato válido.");
        }

        if (!IsValidRoleSet(request.Roles))
        {
            return Validation("RolNoSoportado", "Uno o más roles no pertenecen al catálogo fijo de SGV.");
        }

        var persona = await personaRepository
            .GetByIdAsync(request.PersonaId, cancellationToken)
            .ConfigureAwait(false);
        if (persona is null)
        {
            return Failure(
                UsuarioErrorType.NotFound,
                "PersonaNoEncontrada",
                "La persona asociada al usuario no existe o no está activa.",
                ErrorCategoria.NotFound);
        }

        var result = await identityGateway.CrearAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        await RegistrarAuditoriaAsync(
            result.Value!,
            "Alta",
            EmptyValues,
            CriticalValues(result.Value!),
            cancellationToken).ConfigureAwait(false);

        return result;
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

        if (!IsValidRoleSet(request.Roles))
        {
            return Validation("RolNoSoportado", "Uno o más roles no pertenecen al catálogo fijo de SGV.");
        }

        var previous = await identityGateway.ObtenerAsync(userId, cancellationToken).ConfigureAwait(false);
        if (previous is null)
        {
            return UserNotFound();
        }

        var result = await identityGateway
            .AsignarRolesAsync(userId, request.Roles, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        await RegistrarAuditoriaAsync(
            result.Value!,
            "ModificacionRoles",
            CriticalValues(previous),
            CriticalValues(result.Value!),
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<UsuarioCommandResult> ActualizarAsync(
        string userId,
        ActualizarUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(request.UserName)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return Validation("DatosInvalidos", "Usuario y email son obligatorios.");
        }

        // PR #148 review: la validación de email ahora vive en el
        // helper compartido IsValidEmail. El comportamiento observable
        // (código EmailInvalido + categoria Validation) se mantiene.
        if (!IsValidEmail(request.Email))
        {
            return Validation("EmailInvalido", "El email no tiene un formato válido.");
        }

        if (!IsValidRoleSet(request.Roles))
        {
            return Validation("RolNoSoportado", "Uno o más roles no pertenecen al catálogo fijo de SGV.");
        }

        var previous = await identityGateway.ObtenerAsync(userId, cancellationToken).ConfigureAwait(false);
        if (previous is null)
        {
            return UserNotFound();
        }

        var result = await identityGateway.ActualizarAsync(userId, request, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        await RegistrarAuditoriaAsync(
            result.Value!,
            "Modificacion",
            CriticalValues(previous),
            CriticalValues(result.Value!),
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<UsuarioCommandResult> DesactivarAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("UsuarioRequerido", "El usuario es obligatorio.");
        }

        if (string.Equals(usuarioActual.UserId, userId, StringComparison.Ordinal))
        {
            return Failure(
                UsuarioErrorType.Unauthorized,
                "AutoBaja",
                "No puede desactivar su propio usuario.",
                ErrorCategoria.Forbidden);
        }

        var previous = await identityGateway.ObtenerAsync(userId, cancellationToken).ConfigureAwait(false);
        if (previous is null)
        {
            return UserNotFound();
        }

        var result = await identityGateway.DesactivarAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        await RegistrarAuditoriaAsync(
            previous,
            "BajaLogica",
            CriticalValues(previous),
            EmptyValues,
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<UsuarioCommandResult> ReactivarAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("UsuarioRequerido", "El usuario es obligatorio.");
        }

        var previous = await identityGateway.ObtenerAsync(userId, cancellationToken).ConfigureAwait(false);
        if (previous is null)
        {
            return UserNotFound();
        }

        var persona = await personaRepository
            .GetByIdIncludingDeletedAsync(previous.PersonaId, cancellationToken)
            .ConfigureAwait(false);
        if (persona is null || !persona.IsActive)
        {
            return Failure(
                UsuarioErrorType.Conflict,
                "PersonaInactiva",
                "La persona asociada debe reactivarse antes que el usuario.",
                ErrorCategoria.Conflict);
        }

        var result = await identityGateway.ReactivarAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        await RegistrarAuditoriaAsync(
            result.Value!,
            "Reactivacion",
            EmptyValues,
            CriticalValues(result.Value!),
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyValues =
        new Dictionary<string, object?>();

    private static bool IsValidRoleSet(IReadOnlyCollection<string> roles)
        => roles.Count > 0 && RolesSgv.TodosValidos(roles);

    /// <summary>
    /// PR #148 review: helper compartido entre <see cref="CrearAsync"/>
    /// y <see cref="ActualizarAsync"/> para validar el formato del
    /// email antes de invocar al gateway. <see cref="MailAddress.TryCreate(string, out _)"/>
    /// es la primitiva oficial de .NET para esto y es consistente con
    /// la validación interna de ASP.NET Core Identity.
    /// </summary>
    private static bool IsValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email)
            && MailAddress.TryCreate(email, out _);

    private static IReadOnlyDictionary<string, object?> CriticalValues(UsuarioDto user)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["UserName"] = user.UserName,
            ["Email"] = user.Email,
            ["Roles"] = string.Join(',', user.Roles.OrderBy(role => role, StringComparer.Ordinal))
        };

    private Task RegistrarAuditoriaAsync(
        UsuarioDto user,
        string accion,
        IReadOnlyDictionary<string, object?> anteriores,
        IReadOnlyDictionary<string, object?> nuevos,
        CancellationToken cancellationToken)
        => auditoriaServicio.RegistrarAsync(
            EntidadAuditada,
            user.Id,
            accion,
            usuarioActual.UserId,
            anteriores,
            nuevos,
            cancellationToken);

    private static UsuarioCommandResult Validation(string code, string message)
        => Failure(UsuarioErrorType.Validation, code, message, ErrorCategoria.Validation);

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
}
