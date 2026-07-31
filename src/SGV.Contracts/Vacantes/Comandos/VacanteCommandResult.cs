using SGV.Contracts.Vacantes.Consultas.Dtos;

namespace SGV.Contracts.Vacantes.Comandos;

/// <summary>
/// Resultado uniforme de las mutaciones de vacantes.
/// </summary>
public sealed record VacanteCommandResult(
    bool IsSuccess,
    VacanteDetailDto? Value,
    VacanteError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static VacanteCommandResult Success(VacanteDetailDto value)
        => new(true, value, null);

    public static VacanteCommandResult Failure(VacanteError error)
        => new(false, null, error);

    public static VacanteCommandResult Failure(
        VacanteError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}
