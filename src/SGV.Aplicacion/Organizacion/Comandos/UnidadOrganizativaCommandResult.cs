using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Categorizes command-side failures for organizational units.
/// </summary>
public enum UnidadOrganizativaErrorType
{
    NotFound,
    Conflict,
    Validation
}

/// <summary>
/// Typed error returned by organizational-unit write operations.
/// </summary>
public sealed record UnidadOrganizativaError(
    UnidadOrganizativaErrorType Type,
    string Code,
    string Message
);

/// <summary>
/// Result of an organizational-unit write operation: either a success DTO or a typed error.
/// </summary>
public sealed record UnidadOrganizativaCommandResult(
    bool IsSuccess,
    UnidadOrganizativaDto? Value,
    UnidadOrganizativaError? Error
)
{
    public static UnidadOrganizativaCommandResult Success(UnidadOrganizativaDto value)
        => new(true, value, null);

    public static UnidadOrganizativaCommandResult Failure(UnidadOrganizativaError error)
        => new(false, null, error);
}
