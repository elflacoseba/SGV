using FluentValidation;
using FluentValidation.Results;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Common;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Implements upsert, delete, and list use cases for Cargo-Habilidad assignments.
/// Validates the link-level payload (<see cref="AsignarCargoSkillRequest"/>) via
/// FluentValidation before touching the repositories, applies documented defaults
/// (<c>Ponderacion = 1.00</c>, <c>EsObligatoria = false</c>) when the request omits
/// them, and propagates per-field validation errors via
/// <see cref="CargoSkillCommandResult.FieldErrors"/>.
/// </summary>
public sealed class CargoSkillServicio(
    ICargoRepository cargoRepository,
    IHabilidadRepository habilidadRepository,
    INivelHabilidadRepository nivelHabilidadRepository,
    ICargoSkillRepository skillRepository,
    IUnitOfWork unitOfWork,
    IValidator<AsignarCargoSkillRequest> validator) : ICargoSkillServicio
{
    /// <summary>
    /// Default weight applied to a CargoHabilidad link when the request omits
    /// <see cref="AsignarCargoSkillRequest.Ponderacion"/>. Mirrors
    /// <see cref="Dominio.Habilidades.CargoHabilidad"/>'s minimum required value.
    /// </summary>
    public const decimal PonderacionPorDefecto = 1.00m;

    /// <summary>
    /// Default value applied to <see cref="AsignarCargoSkillRequest.EsObligatoria"/>
    /// when the request omits it.
    /// </summary>
    public const bool EsObligatoriaPorDefecto = false;

    /// <summary>
    /// Convenience constructor that uses the production validator; useful for
    /// tests that want to bypass DI wiring.
    /// </summary>
    public CargoSkillServicio(
        ICargoRepository cargoRepository,
        IHabilidadRepository habilidadRepository,
        INivelHabilidadRepository nivelHabilidadRepository,
        ICargoSkillRepository skillRepository,
        IUnitOfWork unitOfWork)
        : this(
            cargoRepository,
            habilidadRepository,
            nivelHabilidadRepository,
            skillRepository,
            unitOfWork,
            new Validaciones.AsignarCargoSkillRequestValidator())
    {
    }

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
        var validationResult = await validator
            .ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.Validation, "DatosInvalidos",
                    "Uno o más campos del vínculo contienen errores de validación."),
                BuildFieldErrors(validationResult));
        }

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
            .GetByIdAsync(request.NivelRequeridoId, cancellationToken)
            .ConfigureAwait(false);

        if (nivel is null)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.Validation, "NivelHabilidadNoExiste",
                    "El nivel de habilidad referenciado no existe."));
        }

        var ponderacion = request.Ponderacion ?? PonderacionPorDefecto;
        var esObligatoria = request.EsObligatoria ?? EsObligatoriaPorDefecto;

        try
        {
            var existente = await skillRepository
                .GetByCargoAndSkillAsync(cargoId, skillId, cancellationToken)
                .ConfigureAwait(false);

            if (existente is not null)
            {
                // Update existing assignment — CargoHabilidad has no level setter,
                // so we replace via soft approach: remove old, add new.
                // The CargoHabilidad entity is immutable after creation; we delete
                // the old and add a new one to reflect the level change.
                await skillRepository.DeleteAsync(existente, cancellationToken).ConfigureAwait(false);
            }

            var nueva = new CargoHabilidad(cargoId, skillId, request.NivelRequeridoId, ponderacion, esObligatoria)
            {
                Id = Guid.NewGuid()
            };

            await skillRepository.AddAsync(nueva, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return CargoSkillCommandResult.Success(BuildDto(skillId, request.NivelRequeridoId, ponderacion, esObligatoria));
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

            return CargoSkillCommandResult.Success(
                BuildDto(skillId, existente.NivelRequeridoId, existente.Ponderacion, existente.EsObligatoria));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CargoSkillCommandResult.Failure(
                new(CargoSkillErrorType.Validation, "OperacionInvalida", ex.Message));
        }
    }

    private static CargoSkillDto BuildDto(Guid skillId, Guid nivelRequeridoId, decimal ponderacion, bool esObligatoria)
        => new(skillId, nivelRequeridoId)
        {
            Ponderacion = ponderacion,
            EsObligatoria = esObligatoria,
        };

    /// <summary>
    /// Groups FluentValidation failures into a per-field dictionary using
    /// camelCase keys so the HTTP contract matches the JSON casing of the
    /// incoming requests and the eventual <c>ValidationProblemDetails</c>
    /// emitted by the controller. Delegates to the centralized helper to
    /// keep all services in sync.
    /// </summary>
    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(ValidationResult validationResult)
        => ValidationHelper.BuildFieldErrors(validationResult.Errors);
}