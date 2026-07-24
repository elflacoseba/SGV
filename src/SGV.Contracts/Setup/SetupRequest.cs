namespace SGV.Contracts.Setup;

/// <summary>
/// Wire-type payload para el endpoint one-time de setup inicial del
/// primer Administrador (issue #195). Los 9 campos visibles en
/// <c>SGV.Web/Pages/Auth/Setup.cshtml</c> viajan en este record; las
/// reglas de validación FluentValidation viven en
/// <c>SGV.Aplicacion/Setup/Validaciones/SetupRequestValidator</c>, NO
/// aquí — Contracts es leaf.
/// </summary>
public sealed record SetupRequest(
    string Nombres,
    string Apellidos,
    string? Legajo,
    string Email,
    string UserName,
    string Password,
    Guid? TipoDocumentoId,
    string? NumeroDocumento,
    string? Telefono);
