using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    IPasswordResetService passwordResetService,
    IValidator<ForgotPasswordRequest> forgotValidator,
    IValidator<ResetPasswordRequest> resetValidator) : ControllerBase
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
}
