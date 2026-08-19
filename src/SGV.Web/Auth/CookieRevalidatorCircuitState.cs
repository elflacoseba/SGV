namespace SGV.Web.Auth;

/// <summary>
/// Circuit-breaker counter for the cookie revalidation path. Tracks the
/// number of consecutive failures observed by
/// <see cref="CookiePrincipalRevalidator"/> against the API and flips
/// the revalidator to fail-closed once the threshold is reached. After a
/// successful revalidation the counter resets to zero.
/// </summary>
/// <remarks>
/// <para>
/// I-3 release-readiness: previously <see cref="CookiePrincipalRevalidator"/>
/// was pure fail-open — transport or 5xx failures preserved the cookie
/// indefinitely. During a prolonged API outage a user whose account was
/// revoked or blocked kept navigating SGV.Web with a stale bearer. The
/// circuit breaker shortens that window: after
/// <see cref="ConsecutiveFailuresToFailClosed"/> consecutive unreachable
/// outcomes the revalidator starts treating transport failures as hard
/// rejections, forcing the user to re-authenticate.
/// </para>
/// <para>
/// Registered as a singleton so the counter survives across the per-request
/// scoped instances of <see cref="CookiePrincipalRevalidator"/>. State is
/// process-local; in a multi-pod deployment each pod tracks its own
/// counter, but the threshold semantics per pod remain identical.
/// </para>
/// </remarks>
public sealed class CookieRevalidatorCircuitState
{
    /// <summary>
    /// How many consecutive <em>unreachable</em> outcomes (transport failure
    /// or unexpected 5xx) flip the revalidator to fail-closed. The first
    /// hard rejection (401/403/404) does NOT increment this counter — those
    /// are signal, not outage noise — and instead rejects the cookie
    /// immediately via <see cref="CookiePrincipalRevalidator"/>.
    /// </summary>
    public const int ConsecutiveFailuresToFailClosed = 5;

    private int _consecutiveFailures;
    private long _lastUnreachableTicks;

    /// <summary>
    /// Current count of consecutive unreachable outcomes since the last
    /// successful revalidation. Monotonically non-negative; reset by
    /// <see cref="RecordSuccess"/>.
    /// </summary>
    public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);

    /// <summary>
    /// Wall-clock ticks (UTC) of the most recent unreachable outcome, or
    /// zero if the revalidator has not observed a failure yet.
    /// </summary>
    public long LastUnreachableTicks => Volatile.Read(ref _lastUnreachableTicks);

    /// <summary>
    /// Whether the revalidator should treat transport / 5xx failures as
    /// hard rejections. True once <see cref="ConsecutiveFailures"/> has
    /// reached <see cref="ConsecutiveFailuresToFailClosed"/> and stays true
    /// until a successful revalidation resets the counter.
    /// </summary>
    public bool ShouldFailClosed
        => ConsecutiveFailures >= ConsecutiveFailuresToFailClosed;

    /// <summary>
    /// Records a successful API revalidation. Resets the consecutive
    /// failure counter to zero.
    /// </summary>
    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
    }

    /// <summary>
    /// Records an unreachable API outcome (transport failure or unexpected
    /// 5xx). Increments the consecutive failure counter and stamps
    /// <see cref="LastUnreachableTicks"/>.
    /// </summary>
    public void RecordFailure()
    {
        Interlocked.Increment(ref _consecutiveFailures);
        Interlocked.Exchange(ref _lastUnreachableTicks, DateTime.UtcNow.Ticks);
    }
}
