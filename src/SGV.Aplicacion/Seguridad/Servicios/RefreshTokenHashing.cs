using System.Security.Cryptography;
using System.Text;

namespace SGV.Aplicacion.Seguridad.Servicios;

/// <summary>
/// Pure helper that hashes refresh tokens with SHA-256 before they are
/// persisted. The token plain text MUST never reach storage; only this
/// 64-char hex digest does (REQ-RTM-HASH-1).
/// </summary>
/// <remarks>
/// Lives in the application layer because the hashing contract is a
/// pure function — no I/O, no EF Core, no time dependence — and both
/// the infrastructure service that issues refresh tokens and the one
/// that validates them can reuse the same algorithm. PR1a only ships
/// the helper itself; the first real consumer (refresh token issuance
/// from <c>AuthServicio</c>) lands in PR1b.
/// </remarks>
public static class RefreshTokenHashing
{
    /// <summary>
    /// Computes the SHA-256 digest of <paramref name="token"/> over its
    /// UTF-8 bytes and returns the digest formatted as 64 lowercase
    /// hex characters (no separators).
    /// </summary>
    /// <param name="token">Plain refresh token; must not be null.</param>
    /// <returns>64-char lowercase hex string.</returns>
    public static string ComputeSha256Hex(string token)
    {
        // .NET 10's static SHA256.HashData avoids the per-call cost of
        // instantiating an SHA256 instance; combined with the UTF-8 byte
        // allocation for the input this is the lowest-fixed-overhead
        // option available without rolling a Span<char> formatter.
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
