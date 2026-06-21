using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Comandos;

/// <summary>
/// Implements upsert, delete, and list use cases for Persona-Habilidad assignments.
/// Validates that the Persona, Habilidad, and NivelHabilidad exist before persisting.
/// </summary>
public sealed class PersonaSkillServicio(
    IPersonaRepository personaRepository,
    IHabilidadRepository habilidadRepository,
    INivelHabilidadRepository nivelHabilidadRepository,
    IPersonaSkillRepository skillRepository,
    IUnitOfWork unitOfWork) : IPersonaSkillServicio
{
    public async Task<IReadOnlyList<PersonaSkillDetailDto>> ListAsync(
        Guid personaId,
        CancellationToken cancellationToken = default)
    {
        return await skillRepository
            .ListDetailedByPersonaIdAsync(personaId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PersonaSkillCommandResult> UpsertAsync(
        Guid personaId,
        Guid skillId,
        AsignarPersonaSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        var persona = await personaRepository
            .GetByIdForUpdateAsync(personaId, cancellationToken)
            .ConfigureAwait(false);

        if (persona is null)
        {
            return PersonaSkillCommandResult.Failure(
                new(PersonaSkillErrorType.NotFound, "PersonaNoEncontrada", "La persona no existe."));
        }

        var habilidad = await habilidadRepository
            .GetByIdForUpdateAsync(skillId, cancellationToken)
            .ConfigureAwait(false);

        if (habilidad is null)
        {
            return PersonaSkillCommandResult.Failure(
                new(PersonaSkillErrorType.NotFound, "HabilidadNoEncontrada", "La habilidad no existe."));
        }

        var nivel = await nivelHabilidadRepository
            .GetByIdAsync(request.NivelId, cancellationToken)
            .ConfigureAwait(false);

        if (nivel is null)
        {
            return PersonaSkillCommandResult.Failure(
                new(PersonaSkillErrorType.Validation, "NivelHabilidadNoExiste",
                    "El nivel de habilidad referenciado no existe."));
        }

        try
        {
            var existente = await skillRepository
                .GetByPersonaAndSkillAsync(personaId, skillId, cancellationToken)
                .ConfigureAwait(false);

            if (existente is not null)
            {
                // PersonaHabilidad has no level setter, so we replace
                // the assignment via delete + add to reflect the level change.
                await skillRepository.DeleteAsync(existente, cancellationToken).ConfigureAwait(false);
            }

            var nueva = new PersonaHabilidad(personaId, skillId, request.NivelId)
            {
                Id = Guid.NewGuid()
            };

            await skillRepository.AddAsync(nueva, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PersonaSkillCommandResult.Success(new PersonaSkillDto(skillId, request.NivelId));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PersonaSkillCommandResult.Failure(
                new(PersonaSkillErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<PersonaSkillCommandResult> DeleteAsync(
        Guid personaId,
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        var existente = await skillRepository
            .GetByPersonaAndSkillAsync(personaId, skillId, cancellationToken)
            .ConfigureAwait(false);

        if (existente is null)
        {
            return PersonaSkillCommandResult.Failure(
                new(PersonaSkillErrorType.NotFound, "AsociacionNoEncontrada",
                    "La asociación entre la persona y la habilidad no existe."));
        }

        try
        {
            await skillRepository.DeleteAsync(existente, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PersonaSkillCommandResult.Success(new PersonaSkillDto(skillId, existente.NivelHabilidadId));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PersonaSkillCommandResult.Failure(
                new(PersonaSkillErrorType.Validation, "OperacionInvalida", ex.Message));
        }
    }
}
