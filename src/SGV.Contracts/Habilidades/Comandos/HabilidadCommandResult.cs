using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Contracts.Habilidades.Comandos;

/// <summary>
/// Categorizes command-side failures for Habilidad operations.
/// </summary>
public enum HabilidadErrorType
{
    NotFound,
    Conflict,
    Validation,
    /// <summary>
    /// Falla de transporte / servidor (5xx, timeouts de upstream, etc.).
    /// La página web la muestra como error de servidor sin asociarla a un
    /// campo del formulario.
    /// </summary>
    Infrastructure,
    /// <summary>
    /// El <c>CategoriaId</c> informado al crear/actualizar una habilidad
    /// no existe en el catálogo inmutable <c>CategoriasHabilidad</c>
    /// (issue migrar-campo-categoria-habilidades-a-tabla). Se traduce a
    /// <c>400 Bad Request</c> con código <c>CategoriaHabilidadNoExiste</c>.
    /// </summary>
    CategoriaInexistente
}

/// <summary>
/// Typed error returned by Habilidad write operations.
/// </summary>
public sealed record HabilidadError(
    HabilidadErrorType Type,
    string Code,
    string Message,
    int? StatusCode = null,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected);

/// <summary>
/// Result of a Habilidad write operation: either a success DTO or a typed error.
/// </summary>
public sealed record HabilidadCommandResult(
    bool IsSuccess,
    HabilidadDto? Value,
    HabilidadError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null
)
{
    public static HabilidadCommandResult Success(HabilidadDto value)
        => new(true, value, null);

    public static HabilidadCommandResult Failure(HabilidadError error)
        => new(false, null, error);

    public static HabilidadCommandResult Failure(
        HabilidadError error,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(false, null, error, fieldErrors);
}