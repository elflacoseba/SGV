namespace SGV.Contracts.Seguridad.Usuarios;

/// <summary>
/// Body of <c>POST /api/v1/auth/refresh</c>. The refresh token travels
/// in the JSON body — not a cookie — because <see cref="AuthApiClient"/>
/// calls the API server-to-server via <see cref="System.Net.Http.HttpClient"/>,
/// so a <c>Set-Cookie</c> header from the API never reaches the
/// browser. Only <c>SGV.Web</c> persists the refresh cookie (PR3).
/// </summary>
public sealed record RefreshRequest(string RefreshToken);
