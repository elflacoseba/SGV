using SGV.Aplicacion.Personas.Consultas.Dtos;

namespace SGV.Aplicacion.Personas.Comandos;

/// <summary>
/// Categorizes command-side failures for PersonaSkill operations.
/// </summary>
public enum PersonaSkillErrorType
{
    NotFound,
    Validation
}

/// <summary>
/// Typed error returned by PersonaSkill write operations.
/// </summary>
public sealed record PersonaSkillError(
    PersonaSkillErrorType Type,
    string Code,
    string Message
);

/// <summary>
/// Result of a PersonaSkill write operation: either a success DTO or a typed error.
/// </summary>
public sealed record PersonaSkillCommandResult(
    bool IsSuccess,
    PersonaSkillDto? Value,
    PersonaSkillError? Error
)
{
    public static PersonaSkillCommandResult Success(PersonaSkillDto value)
        => new(true, value, null);

    public static PersonaSkillCommandResult Failure(PersonaSkillError error)
        => new(false, null, error);
}
