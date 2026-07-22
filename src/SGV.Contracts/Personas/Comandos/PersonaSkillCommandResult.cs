using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Categorizes command-side failures for PersonaSkill operations.
/// </summary>
/// <remarks>
/// Variantes cerradas (slice 1 / decision #1284). <c>NotFound</c> se mapea
/// a <see cref="ErrorCategoria.NotFound"/> (HTTP 404),
/// <c>Validation</c> a <see cref="ErrorCategoria.Validation"/> (HTTP 400).
/// Variantes nuevas caen en <see cref="ErrorCategoria.Unexpected"/> o
/// <see cref="ErrorCategoria.Transport"/> vía mapper común; este enum NO
/// se reintroduce como discriminador público al cliente web.
/// </remarks>
public enum PersonaSkillErrorType
{
    NotFound,
    Validation
}

/// <summary>
/// Typed error returned by PersonaSkill write operations. Mirrors the
/// shape used by sibling subdomains (CargoSkill, Habilidad, Persona):
/// expone <see cref="Categoria"/> además de <see cref="Type"/> para que
/// el cliente web pueda ramificar por la taxonomía común
/// <see cref="ErrorCategoria"/> sin consultar el enum del subdominio.
/// </summary>
public sealed record PersonaSkillError(
    PersonaSkillErrorType Type,
    string Code,
    string Message,
    int? StatusCode = null,
    ErrorCategoria Categoria = ErrorCategoria.Unexpected);

/// <summary>
/// Result of a PersonaSkill write operation: either a success DTO or a
/// typed error. El shape observable de las respuestas JSON MUST
/// preservarse respecto al contrato vigente.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FieldErrors"/> se popula cuando el backend responde
/// <c>400 Bad Request</c> con <c>ValidationProblemDetails</c>: cada
/// entrada del diccionario <c>errors</c> del backend se mapea a una
/// entrada <c>(key, mensajes[])</c>. La Razor Page de Slice 3a usa esa
/// estructura para reflejar los errores junto al input correspondiente.
/// </para>
/// <para>
/// La sobrecarga <see cref="Failure(PersonaSkillError, IReadOnlyDictionary{string, string[]}?)"/>
/// preserva source-compat con los call sites que aún no propagan
/// FieldErrors (Slice 1 ya la consumía con la sobrecarga simple).
/// </para>
/// </remarks>
public sealed record PersonaSkillCommandResult(
    bool IsSuccess,
    PersonaSkillDto? Value,
    PersonaSkillError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static PersonaSkillCommandResult Success(PersonaSkillDto value)
        => new(true, value, null);

    public static PersonaSkillCommandResult Failure(PersonaSkillError error)
        => new(false, null, error);

    /// <summary>
    /// Construye un Failure preservando los <c>FieldErrors</c> del
    /// <c>ValidationProblemDetails</c> recibido del backend. La Razor
    /// Page los proyecta a <c>ModelState</c> con el prefijo del campo
    /// para que aparezcan junto al input correspondiente.
    /// </summary>
    public static PersonaSkillCommandResult Failure(
        PersonaSkillError error,
        IReadOnlyDictionary<string, string[]>? fieldErrors)
        => new(false, null, error, fieldErrors);
}
