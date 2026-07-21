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
public sealed record PersonaSkillCommandResult(
    bool IsSuccess,
    PersonaSkillDto? Value,
    PersonaSkillError? Error)
{
    public static PersonaSkillCommandResult Success(PersonaSkillDto value)
        => new(true, value, null);

    public static PersonaSkillCommandResult Failure(PersonaSkillError error)
        => new(false, null, error);
}
