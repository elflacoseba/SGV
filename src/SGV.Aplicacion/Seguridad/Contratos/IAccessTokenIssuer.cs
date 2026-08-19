namespace SGV.Aplicacion.Seguridad.Contratos;

/// <summary>
/// Port that mints the access JWT for a user. Extracted from
/// <c>AuthServicio</c> in PR2a (change <c>implementa-refresh-tokens</c>)
/// so the login flow and the refresh flow mint structurally identical
/// tokens instead of duplicating claim assembly.
/// </summary>
/// <remarks>
/// The claim set is unchanged by this change (design §2.7): no <c>jti</c>
/// and no <c>family_id</c> claim is added. Revocation is handled entirely
/// server-side through the refresh token family.
/// </remarks>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// Issues an access JWT for <paramref name="userId"/>, or <c>null</c>
    /// when the user no longer exists.
    /// </summary>
    Task<AccessTokenEmitido?> EmitirAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Access token plus its absolute expiration.
/// </summary>
public sealed record AccessTokenEmitido(string AccessToken, DateTimeOffset ExpiresAt);
