using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.RateLimiting;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Aplicacion.Seguridad.PasswordChange;
using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Contracts.Auth;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Api.Controllers;

[ApiController]
[Route(AuthApiRoutes.Base)]
[Produces("application/json")]
public sealed class AuthController(
    IAuthServicio authServicio,
    IRefreshTokenServicio refreshTokenServicio,
    IPasswordResetService passwordResetService,
    IChangePasswordService changePasswordService,
    IValidator<ForgotPasswordRequest> forgotValidator,
    IValidator<ResetPasswordRequest> resetValidator,
    IValidator<ValidateResetTokenRequest> validateTokenValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator) : ControllerBase
{
    [HttpPost(AuthApiRoutes.LoginRelative)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authServicio.LoginAsync(request, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    /// <summary>
    /// Rota un refresh token de uso único y devuelve un par
    /// access+refresh nuevo (REQ-AUTH-REFRESH-1). El token viaja en el
    /// body, no en una cookie: la API es cookie-agnóstica y
    /// <c>SGV.Web</c> es el único emisor de <c>sgv.rt</c>
    /// (design §2.6). Los tres modos de falla — token inexistente,
    /// expirado y replay — colapsan a <c>401</c> para no filtrar el
    /// estado del token al cliente; el replay además revoca la familia
    /// completa server-side antes de responder.
    /// </summary>
    [HttpPost(AuthApiRoutes.RefreshRelative)]
    [AllowAnonymous]
    [EnableRateLimiting(AuthApiRoutes.RefreshPolicyName)]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var result = await refreshTokenServicio
            .RefreshAsync(request?.RefreshToken, cancellationToken)
            .ConfigureAwait(false);

        if (result.Outcome != RefreshOutcome.Success)
        {
            return Unauthorized(new { mensaje = "La sesión expiró. Iniciá sesión nuevamente." });
        }

        return Ok(new RefreshResponse(
            result.AccessToken!,
            result.ExpiresAt!.Value,
            result.RefreshToken!,
            result.RefreshTokenExpiresAt!.Value));
    }

    /// <summary>
    /// Revoca server-side todos los refresh tokens activos del usuario
    /// autenticado (REQ-AUTH-LOGOUT-1). Idempotente: una sesión legacy
    /// sin refresh token responde <c>200</c> igual. No emite cookies —
    /// la limpieza de <c>sgv.auth</c> y <c>sgv.rt</c> es responsabilidad
    /// de <c>SGV.Web</c>.
    /// </summary>
    [HttpPost(AuthApiRoutes.LogoutRelative)]
    [Authorize]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
        {
            // [Authorize] ya garantizó identidad, pero defensa en profundidad.
            return Unauthorized();
        }

        await refreshTokenServicio
            .RevokeAsync(userId, request?.RefreshToken, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new LogoutResponse(true));
    }

    /// <summary>
    /// Cambia la contraseña del usuario autenticado. El endpoint rota
    /// además el <c>SecurityStamp</c> para invalidar cookies y bearer
    /// vigentes (issue #204 PR2). La política rate limit
    /// <c>ChangePassword</c> (5 req / 15 min) se keyed por subject y
    /// MUST correr DESPUÉS de <c>[Authorize]</c>.
    /// </summary>
    [HttpPost(AuthApiRoutes.ChangePasswordRelative)]
    [Authorize]
    [EnableRateLimiting(AuthApiRoutes.ChangePasswordPolicyName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { mensaje = "El cuerpo de la solicitud es obligatorio." });
        }

        var validation = await changePasswordValidator
            .ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
        {
            // [Authorize] ya garantizó identidad, pero defensa en profundidad.
            return Unauthorized();
        }

        var outcome = await changePasswordService
            .ChangePasswordAsync(userId, request, cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            ChangePasswordOutcome.Success =>
                Ok(new { mensaje = "Tu contraseña fue actualizada." }),
            ChangePasswordOutcome.InvalidCurrentPassword =>
                BadRequest(new { mensaje = "La contraseña actual no es correcta." }),
            ChangePasswordOutcome.ValidationError =>
                BadRequest(new { mensaje = "La nueva contraseña no cumple la política de seguridad." }),
            ChangePasswordOutcome.RateLimited =>
                StatusCode(StatusCodes.Status429TooManyRequests),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost(AuthApiRoutes.ForgotPasswordRelative)]
    [AllowAnonymous]
    [EnableRateLimiting(AuthApiRoutes.ForgotPasswordPolicyName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { mensaje = "El cuerpo de la solicitud es obligatorio." });
        }

        var validation = await forgotValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        // The service collapses UserNotFound into Success
        // (anti-enumeration); any other outcome is a programmer error
        // and bubbles up as 500 via ProblemDetails.
        _ = await passwordResetService.ForgotPasswordAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(new { mensaje = "Si la cuenta existe, te enviamos un correo para restablecer la contraseña." });
    }

    [HttpPost(AuthApiRoutes.ResetPasswordRelative)]
    [AllowAnonymous]
    [EnableRateLimiting(AuthApiRoutes.ResetPasswordPolicyName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { mensaje = "El cuerpo de la solicitud es obligatorio." });
        }

        var validation = await resetValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var outcome = await passwordResetService.ResetPasswordAsync(request, cancellationToken).ConfigureAwait(false);
        if (outcome == PasswordResetOutcome.InvalidToken)
        {
            return BadRequest(new { mensaje = "El enlace de restablecimiento no es válido o ya expiró." });
        }

        return Ok(new { mensaje = "Tu contraseña fue actualizada." });
    }

    [HttpPost(AuthApiRoutes.ValidateResetTokenRelative)]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateResetToken(
        ValidateResetTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { mensaje = "El cuerpo de la solicitud es obligatorio." });
        }

        var validation = await validateTokenValidator
            .ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var outcome = await passwordResetService
            .ValidateResetTokenAsync(request.UserId, request.Token, cancellationToken)
            .ConfigureAwait(false);

        return outcome == PasswordResetOutcome.InvalidToken
            ? BadRequest(new { mensaje = "El enlace de restablecimiento no es válido o ya expiró." })
            : Ok(new { mensaje = "El token es válido." });
    }
}
