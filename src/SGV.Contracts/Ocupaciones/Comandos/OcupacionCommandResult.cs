using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Dtos;

namespace SGV.Contracts.Ocupaciones.Comandos;

[Obsolete("Use SGV.Contracts.Comun.ErrorCategoria.")]
public enum OcupacionErrorType
{
    NotFound,
    Conflict,
    Validation
}

#pragma warning disable CS0618
/// <summary>
/// Error payload for OcupacionCommandResult.
/// </summary>
/// <remarks>
/// The primary constructor that accepts <see cref="OcupacionErrorType"/> is
/// obsolete and remains only for backward compatibility while the domain
/// migrates to <see cref="ErrorCategoria"/>. New code must use the
/// <see cref="OcupacionError(ErrorCategoria, string, string)"/> constructor.
/// </remarks>
public sealed record OcupacionError(
    OcupacionErrorType Type,
    string Code,
    string Message,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected)
{
    public OcupacionError(ErrorCategoria categoria, string code, string message)
        : this(ErrorCategoriaMappers.ToTipoOcupacion(categoria), code, message, categoria)
    {
    }
}
#pragma warning restore CS0618

public sealed record OcupacionCommandResult(
    bool IsSuccess,
    OcupacionDto? Value,
    OcupacionError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static OcupacionCommandResult Success(OcupacionDto value)
        => new(true, value, null);

    public static OcupacionCommandResult Failure(OcupacionError error)
        => new(false, null, error);

    public static OcupacionCommandResult Failure(
        OcupacionError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
