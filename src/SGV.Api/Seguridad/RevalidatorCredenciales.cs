using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGV.Infraestructura.Seguridad;

namespace SGV.Api.Seguridad;

/// <summary>
/// Revalidates an authenticated Identity account against its current
/// lockout state.
/// </summary>
public interface IRevalidatorCredenciales
{
    /// <summary>
    /// Determines whether the account still exists and is not locked out.
    /// </summary>
    /// <param name="userId">The Identity user identifier from the credential subject.</param>
    /// <param name="cancellationToken">Token used to cancel the validation request.</param>
    /// <returns><see langword="true"/> when the account can continue, otherwise <see langword="false"/>.</returns>
    Task<bool> SigueVigenteAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves a fresh Identity scope for each credential validation so a
/// singleton authentication hook never captures a request-scoped context.
/// </summary>
public sealed class RevalidatorCredenciales(
    IServiceScopeFactory scopeFactory,
    ILogger<RevalidatorCredenciales> logger) : IRevalidatorCredenciales
{
    /// <summary>
    /// Marker used by the API fallback middleware to avoid repeating a
    /// validation already completed by the JWT bearer event.
    /// </summary>
    internal const string ValidationMarker = "Sgv.CredentialRevalidation.Completed";

    /// <inheritdoc />
    public async Task<bool> SigueVigenteAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);

        if (user is null)
        {
            // RES-003 / REA-007: surface these — a token surviving a deleted account is a security event.
            logger.LogInformation(
                "Credential rejected because user {UserId} no longer exists.",
                userId);
            return false;
        }

        var isLockedOut = await userManager.IsLockedOutAsync(user).ConfigureAwait(false);
        if (isLockedOut)
        {
            logger.LogInformation(
                "Credential rejected because user {UserId} is locked out.",
                userId);
        }

        return !isLockedOut;
    }
}
