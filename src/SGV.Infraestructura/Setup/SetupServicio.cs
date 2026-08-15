using System.Net.Mail;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Common;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Contracts.Setup;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Seguridad;

namespace SGV.Infraestructura.Setup;

/// <summary>
/// Orquestador one-time del primer Administrador (issue #195).
/// Encapsula la transacción EF que crea <c>Persona</c>,
/// <c>AspNetUsers</c>, <c>AspNetUserRoles</c> y la fila de
/// <c>Auditorias</c> con <c>userId="system"</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Atomicidad — desviación del design §3.3.</b> Pomelo 9 +
/// MySqlConnector rechazan <c>BeginTransactionAsync</c> anidados;
/// la transacción outer no se abre y la atomicidad se logra por
/// compensación (soft-delete sobre Persona si Usuario falla).
/// Documentado en <c>docs/decisiones-implementacion.md</c>
/// §"Setup inicial" (issue #195 follow-up).
/// </para>
/// <para>
/// <b>Concurrencia.</b> Defensa contra doble admin simultáneo: el
/// índice único <c>IX_AspNetUsers_NormalizedUserName</c> rechaza el
/// segundo <c>INSERT</c> vía <c>IdentityError.DuplicateUserName</c>
/// (decisión design §2.1). La guarda <c>AnyUsersAsync</c> se ejecuta
/// antes de delegar al gateway para cerrar la ventana entre intentos.
/// </para>
/// <para>
/// <b>Auditoría.</b> <see cref="IAuditoriaServicio.RegistrarAsync"/>
/// recibe <c>usuarioOperadorId="system"</c> explícito. La
/// implementación vigente (<c>AuditoriaServicio</c>) prefiere este
/// valor sobre <c>IUsuarioActual.UserId</c> cuando está poblado.
/// </para>
/// </remarks>
public sealed class SetupServicio(
    UserManager<SgvIdentityUser> userManager,
    SgvDbContext context,
    IPersonaServicioComandos personaServicio,
    IUsuarioIdentityGateway identityGateway,
    IAuditoriaServicio auditoriaServicio,
    IValidator<SetupRequest> validator,
    ILogger<SetupServicio> logger) : ISetupServicio
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyValues =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public async Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        // Read-only. AnyAsync contra AspNetUsers se traduce a un EXISTS
        // contra la PK clustered (O(1)).
        var anyUsers = await userManager.Users.AnyAsync(ct).ConfigureAwait(false);
        return new SetupStatusResponse(RequiresSetup: !anyUsers);
    }

    public async Task<SetupCommandResult> CrearAdminAsync(
        SetupRequest request,
        CancellationToken ct = default)
    {
        // 1) FluentValidation previa al gateway de Identity / DB.
        var validation = await validator.ValidateAsync(request, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return SetupCommandResult.Failure(
                new SetupError(
                    ErrorCategoria.Validation,
                    SetupErrorCode.DatosInvalidos,
                    "Uno o más campos contienen errores de validación.",
                    StatusCode: 400),
                ValidationHelper.BuildFieldErrors(validation.Errors));
        }

        // 2) Guarda `AnyUsers` antes de delegar al gateway. No abre
        //    transacción outer (ver remarks).
        var anyUsers = await userManager.Users.AnyAsync(ct).ConfigureAwait(false);
        if (anyUsers)
        {
            return SetupCommandResult.Failure(
                new SetupError(
                    ErrorCategoria.Conflict,
                    SetupErrorCode.SetupYaCompletado,
                    "La configuración inicial ya fue completada.",
                    StatusCode: 409));
        }

        // 3) Crear Persona vía servicio de aplicación (validación +
        //    unicidad de Legajo / Email / Documento ya implementadas).
        var personaRequest = new CrearPersonaRequest(
            Legajo: request.Legajo,
            Nombres: request.Nombres,
            Apellidos: request.Apellidos,
            Email: request.Email,
            TipoDocumentoId: request.TipoDocumentoId,
            NumeroDocumento: request.NumeroDocumento,
            Telefono: request.Telefono);

        PersonaCommandResult personaResult;
        try
        {
            personaResult = await personaServicio.CrearAsync(personaRequest, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var sanitizedUserName = (request.UserName ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);

            logger.LogError(ex,
                "Setup inicial falló durante la creación de Persona (UserName={UserName})",
                sanitizedUserName);
            return SetupCommandResult.Failure(TransaccionFallida("No se pudo crear la persona administradora."));
        }

        if (!personaResult.IsSuccess)
        {
            return SetupCommandResult.Failure(
                MapPersonaError(personaResult.Error!),
                personaResult.FieldErrors);
        }

        // 4) Crear Usuario vía gateway de Identity. El gateway abre
        //    su propia transacción atómica (AspNetUsers + roles).
        var usuarioRequest = new CrearUsuarioRequest(
            PersonaId: personaResult.Value!.Id,
            UserName: request.UserName,
            Email: request.Email,
            Password: request.Password,
            Roles: new[] { RolesSgv.Administrador });

        UsuarioCommandResult usuarioResult;
        try
        {
            usuarioResult = await identityGateway.CrearAsync(usuarioRequest, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await CompensatePersonaAsync(personaResult.Value.Id, ct).ConfigureAwait(false);
            logger.LogError(ex,
                "Setup inicial falló durante la creación de Usuario (PersonaId={PersonaId}): {Message}",
                personaResult.Value.Id, ex.Message);
            return SetupCommandResult.Failure(TransaccionFallida($"No se pudo crear el usuario administrador: {ex.GetType().Name} - {ex.Message}"));
        }

        if (!usuarioResult.IsSuccess)
        {
            // Compensación: la Persona fue creada (commit en personaServicio)
            // pero el Usuario falló. Soft-delete la Persona para no dejar
            // una Persona huérfana sin Usuario.
            await CompensatePersonaAsync(personaResult.Value.Id, ct).ConfigureAwait(false);
            return SetupCommandResult.Failure(
                MapUsuarioError(usuarioResult.Error!),
                usuarioResult.FieldErrors);
        }

        // 5) Auditoría explícita con userId="system". Si la auditoría
        //    falla, NO deshacemos el setup (el admin ya está creado y
        //    puede firmar); sólo loggeamos el fallo.
        try
        {
            await auditoriaServicio.RegistrarAsync(
                entidad: "SetupInicial",
                entityId: usuarioResult.Value!.Id,
                accion: "AltaPrimerAdministrador",
                usuarioOperadorId: "system",
                valoresAnteriores: EmptyValues,
                valoresNuevos: BuildAuditValues(usuarioResult.Value, personaResult.Value),
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Setup inicial completó la creación de Persona+Usuario pero la auditoría falló (UserId={UserId})",
                usuarioResult.Value!.Id);
        }

        return SetupCommandResult.Success(new SetupResult(
            PersonaId: personaResult.Value.Id,
            UserId: usuarioResult.Value.Id,
            UserName: usuarioResult.Value.UserName));
    }

    /// <summary>
    /// Compensación tras un fallo de Identity: soft-delete la Persona
    /// recién creada para no dejarla huérfana (dev §3.3 - atomicidad
    /// best-effort). Las Personas en soft-delete NO cuentan para
    /// futuras invocaciones del setup porque la guarda es contra
    /// <c>AspNetUsers</c>, no contra <c>Personas</c>.
    /// </summary>
    private async Task CompensatePersonaAsync(Guid personaId, CancellationToken ct)
    {
        try
        {
            await personaServicio.DesactivarAsync(personaId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Compensación de Persona falló durante rollback de Setup (PersonaId={PersonaId})",
                personaId);
        }
    }

    private static SetupError TransaccionFallida(string detail)
        => new(
            ErrorCategoria.Unexpected,
            SetupErrorCode.TransaccionFallida,
            detail,
            StatusCode: 500);

    /// <summary>
    /// Traduce un <see cref="PersonaError"/> al <see cref="SetupError"/>
    /// correspondiente. Conserva <see cref="PersonaError.Categoria"/> y
    /// traduce el código canónico a un <see cref="SetupErrorCode"/>
    /// equivalente; el resto de códigos colapsa a
    /// <see cref="SetupErrorCode.ValidacionIdentity"/>.
    /// </summary>
    private static SetupError MapPersonaError(PersonaError error)
    {
        var setupCode = error.Code switch
        {
            "LegajoDuplicado" => SetupErrorCode.LegajoDuplicado,
            "EmailDuplicado" => SetupErrorCode.EmailDuplicado,
            "DocumentoDuplicado" => SetupErrorCode.DocumentoDuplicado,
            "DatosInvalidos" => SetupErrorCode.DatosInvalidos,
            _ => SetupErrorCode.ValidacionIdentity,
        };

        return new SetupError(
            error.Categoria,
            setupCode,
            error.Message,
            StatusCode: error.StatusCode ?? MapCategoria(error.Categoria));
    }

    /// <summary>
    /// Traduce un <see cref="UsuarioError"/> al <see cref="SetupError"/>
    /// correspondiente. Mapea los códigos estables que ya emite
    /// <see cref="UsuarioIdentityGateway.ToIdentityFailure"/> a un
    /// <see cref="SetupErrorCode"/> homólogo.
    /// </summary>
    private static SetupError MapUsuarioError(UsuarioError error)
    {
        var setupCode = error.Code switch
        {
            "UserNameDuplicado" => SetupErrorCode.UserNameDuplicado,
            "EmailDuplicado" => SetupErrorCode.EmailDuplicado,
            "PersonaYaTieneUsuario" => SetupErrorCode.PersonaConUsuario,
            "InvalidEmail" => SetupErrorCode.EmailInvalido,
            "InvalidUserName" => SetupErrorCode.UserNameInvalido,
            "PasswordTooShort" => SetupErrorCode.PasswordDebil,
            "PasswordRequiresNonAlphanumeric" => SetupErrorCode.PasswordDebil,
            "PasswordRequiresDigit" => SetupErrorCode.PasswordDebil,
            "PasswordRequiresLower" => SetupErrorCode.PasswordDebil,
            "PasswordRequiresUpper" => SetupErrorCode.PasswordDebil,
            "PasswordRequiresUniqueChars" => SetupErrorCode.PasswordDebil,
            "IdentityError" => SetupErrorCode.ValidacionIdentity,
            _ => SetupErrorCode.ValidacionIdentity,
        };

        return new SetupError(
            error.Categoria,
            setupCode,
            error.Message,
            StatusCode: error.StatusCode ?? MapCategoria(error.Categoria));
    }

    private static int MapCategoria(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.Validation => 400,
        ErrorCategoria.Conflict => 409,
        ErrorCategoria.NotFound => 404,
        ErrorCategoria.Unauthorized => 401,
        ErrorCategoria.Forbidden => 403,
        ErrorCategoria.Transport => 503,
        ErrorCategoria.Unexpected => 500,
        _ => 500,
    };

    private static IReadOnlyDictionary<string, object?> BuildAuditValues(
        UsuarioDto usuario,
        PersonaDto persona)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["UserId"] = usuario.Id,
            ["UserName"] = usuario.UserName,
            ["Email"] = usuario.Email,
            ["Roles"] = string.Join(',', usuario.Roles.OrderBy(role => role, StringComparer.Ordinal)),
            ["PersonaId"] = persona.Id,
            ["PersonaNombres"] = persona.Nombres,
            ["PersonaApellidos"] = persona.Apellidos,
        };
    }
}
