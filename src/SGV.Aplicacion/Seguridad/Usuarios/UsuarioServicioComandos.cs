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

    public async Task<UsuarioCommandResult> BloquearAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("UsuarioRequerido", "El usuario es obligatorio.");
        }

        // Auto-bloqueo prohibido: no podés bloquearte a vos mismo.
        if (string.Equals(usuarioActual.UserId, userId, StringComparison.Ordinal))
        {
            return Failure(
                UsuarioErrorType.Unauthorized,
                "AutoBloqueo",
                "No puede bloquear su propio usuario.",
                ErrorCategoria.Forbidden);
        }

        var previous = await identityGateway.ObtenerAsync(userId, cancellationToken).ConfigureAwait(false);
        if (previous is null)
        {
            return UserNotFound();
        }

        var result = await identityGateway.BloquearAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        // RIS-004 (4R review): capturar estado de seguridad previo en la
        // auditoría; antes era EmptyValues, lo que perdía la transición
        // `Bloqueado=false → Bloqueado=true`. previous.Bloqueado ya viene
        // poblado por Corr 4 (UsuarioIdentityGateway.MapAsync).
        await RegistrarAuditoriaAsync(
            result.Value!,
            "BloqueoUsuario",
            CriticalValues(previous),
            CriticalValues(result.Value!),
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<UsuarioCommandResult> DesbloquearAsync(
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

        var result = await identityGateway.DesbloquearAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        // RIS-004: análogo a BloquearAsync.
        await RegistrarAuditoriaAsync(
            result.Value!,
            "DesbloqueoUsuario",
            CriticalValues(previous),
            CriticalValues(result.Value!),
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<UsuarioCommandResult> EliminarAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Validation("UsuarioRequerido", "El usuario es obligatorio.");
        }

        // Auto-eliminación prohibida: no podés borrarte a vos mismo.
        if (string.Equals(usuarioActual.UserId, userId, StringComparison.Ordinal))
        {
            return Failure(
                UsuarioErrorType.Unauthorized,
                "AutoEliminacion",
                "No puede eliminar su propio usuario.",
                ErrorCategoria.Forbidden);
        }

        var previous = await identityGateway.ObtenerAsync(userId, cancellationToken).ConfigureAwait(false);
        if (previous is null)
        {
            return UserNotFound();
        }

        // RES-002 (4R review): auditar ANTES del delete físico. El auditor
        // ya persiste via SaveChangesAsync(); si DeleteAsync falla
        // downstream la fila de Auditoria queda registrada, satisfaciendo
        // el requisito de trazabilidad del intento de eliminación.
        await RegistrarAuditoriaAsync(
            previous,
            "EliminacionFisica",
            CriticalValues(previous),
            EmptyValues,
            cancellationToken).ConfigureAwait(false);

        return await identityGateway.EliminarAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    [Obsolete("Use BloquearAsync. El controller rediseña en Phase 2.")]
    public Task<UsuarioCommandResult> DesactivarAsync(string userId, CancellationToken cancellationToken = default)
        => BloquearAsync(userId, cancellationToken);

    [Obsolete("Use DesbloquearAsync. El controller rediseña en Phase 2.")]
    public Task<UsuarioCommandResult> ReactivarAsync(string userId, CancellationToken cancellationToken = default)
        => DesbloquearAsync(userId, cancellationToken);

    private static readonly IReadOnlyDictionary<string, object?> EmptyValues =
        new Dictionary<string, object?>();

    private static bool IsValidRoleSet(IReadOnlyCollection<string> roles)
        => roles.Count > 0 && RolesSgv.TodosValidos(roles);

    private static bool IsValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email)
            && MailAddress.TryCreate(email, out _);

    private static IReadOnlyDictionary<string, object?> CriticalValues(UsuarioDto user)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["UserName"] = user.UserName,
            ["Email"] = user.Email,
            ["Roles"] = string.Join(',', user.Roles.OrderBy(role => role, StringComparer.Ordinal)),
            // RIS-004 (4R review): capturar Bloqueado en la auditoría para
            // distinguir transiciones lockout ⇒ unlock en el log de cambios.
            // LockoutEnd / LockoutEnabled / AccessFailedCount (más profundos)
            // requieren un nuevo gateway method y se difieren a Phase 2
            // para no inflar el budget del bounded correction transaction.
            ["Bloqueado"] = user.Bloqueado
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