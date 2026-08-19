namespace SGV.Contracts.Seguridad.Usuarios;

/// <summary>
/// Body of <c>POST /api/v1/auth/logout</c>. The refresh token is optional:
/// legacy sessions created before the refresh flow existed have no token to
/// present and MUST still log out successfully (REQ-AUTH-LOGOUT-1,
/// scenario "logout sin refresh cookie").
/// </summary>
public sealed record LogoutRequest(string? RefreshToken = null);

/// <summary>
/// Response of <c>POST /api/v1/auth/logout</c>.
/// </summary>
public sealed record LogoutResponse(bool Success);
