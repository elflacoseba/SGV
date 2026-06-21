using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Implements upsert, delete, and list use cases for Cargo-Habilidad assignments.
/// Validates that the Cargo, Habilidad, and NivelHabilidad exist before persisting.
/// </summary>
public sealed class CargoSkillServicio(
    ICargoRepository cargoRepository,
    IHabilidadRepository habilidadRepository,
    INivelHabilidadRepository nivelHabilidadRepository,
    ICargoSkillRepository skillRepository,
    IUnitOfWork unitOfWork) : ICargoSkillServicio
{
    public async Task<IReadOnlyList<CargoSkillDetailDto>> ListAsync(
        Guid cargoId,
        CancellationToken cancellationToken = default)
    {
        return await skillRepository
            .ListDetailedByCargoIdAsync(cargoId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CargoSkillCommandResult> UpsertAsync(
        Guid cargoId,
        Guid skillId,
        AsignarCargoSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        var cargo = await cargoRepository
            .GetByIdForUpdateAsync(cargoId, cancellationToken)
            .ConfigureAwait(false);

        if (cargo is null)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe."));
        }

        var habilidad = await habilidadRepository
            .GetByIdForUpdateAsync(skillId, cancellationToken)
            .ConfigureAwait(false);

        if (habilidad is null)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.NotFound, "HabilidadNoEncontrada", "La habilidad no existe."));
        }

        var nivel = await nivelHabilidadRepository
            .GetByIdAsync(request.NivelId, cancellationToken)
            .ConfigureAwait(false);

        if (nivel is null)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.Validation, "NivelHabilidadNoExiste",
                    "El nivel de habilidad referenciado no existe."));
        }

        try
        {
            var existente = await skillRepository
                .GetByCargoAndSkillAsync(cargoId, skillId, cancellationToken)
                .ConfigureAwait(false);

            if (existente is not null)
            {
                // Update existing assignment — CargoHabilidad has no level setter,
                // so we replace via soft approach: remove old, add new.
                // For now, we simply persist the new level via domain logic expansion.
                // The CargoHabilidad entity is immutable after creation; we delete
                // the old and add a new one to reflect the level change.
                await skillRepository.DeleteAsync(existente, cancellationToken).ConfigureAwait(false);
            }

            var nueva = new CargoHabilidad(cargoId, skillId, request.NivelId, 1.0m, false)
            {
                Id = Guid.NewGuid()
            };

            await skillRepository.AddAsync(nueva, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return CargoSkillCommandResult.Success(new CargoSkillDto(skillId, request.NivelId));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<CargoSkillCommandResult> DeleteAsync(
        Guid cargoId,
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        var existente = await skillRepository
            .GetByCargoAndSkillAsync(cargoId, skillId, cancellationToken)
            .ConfigureAwait(false);

        if (existente is null)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.NotFound, "AsociacionNoEncontrada",
                    "La asociación entre el cargo y la habilidad no existe."));
        }

        try
        {
            await skillRepository.DeleteAsync(existente, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return CargoSkillCommandResult.Success(new CargoSkillDto(skillId, existente.NivelRequeridoId));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.Validation, "OperacionInvalida", ex.Message));
        }
    }
}
