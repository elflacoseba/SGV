using SGV.Contracts.Comun;

namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Typed error returned by Persona write operations. Mirrors the shape used by
/// other subdomains (Cargo) including the <see cref="ErrorCategoria"/> slot so the
/// web shell can branch on the common taxonomy.
/// </summary>
public sealed record PersonaError(
    PersonaErrorType Type,
    string Code,
    string Message,
    int? StatusCode = null,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected);