using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Dtos;

namespace SGV.Contracts.Ocupaciones.Comandos;

/// <summary>
/// Error payload for OcupacionCommandResult.
/// </summary>
/// <remarks>
/// Post-housekeeping (change <c>ocupaciones-housekeeping-release</c>) el
/// record consume únicamente <see cref="ErrorCategoria"/> como taxonomía de
/// error. La rama legacy con <c>OcupacionErrorType</c> quedó completamente
/// removida del grafo (ver decisiones-implementacion.md §Ocupaciones release-ready).
/// </remarks>
public sealed record OcupacionError(
    ErrorCategoria Categoria,
    string Code,
    string Message);

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
