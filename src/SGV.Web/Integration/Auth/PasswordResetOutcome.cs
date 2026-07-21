namespace SGV.Web.Integration.Auth;

/// <summary>
/// Outcome returned by the anonymous password recovery operations.
/// </summary>
public enum PasswordResetOutcome
{
    /// <summary>
    /// The request completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The upstream API rejected the request because the caller exceeded its rate limit.
    /// </summary>
    RateLimited = 1,

    /// <summary>
    /// The upstream API rejected the reset token.
    /// </summary>
    InvalidToken = 2
}
