using SGV.Contracts.Comun;

namespace SGV.Contracts.Vacantes.Comandos;

/// <summary>
/// Error payload para <see cref="VacanteCommandResult"/>.
/// </summary>
/// <remarks>
/// Usa <see cref="ErrorCategoria"/> canónico sin deuda de tipos legacy.
/// </remarks>
public sealed record VacanteError(
    ErrorCategoria Categoria,
    string Code,
    string Message);
