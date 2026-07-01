using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Implements create, update, soft-delete, and reactivate use cases for Cargo.
/// </summary>
public sealed class CargoServicioComandos(
    ICargoRepository repository,
    INivelCargoRepository nivelCargoRepository,
    IUnitOfWork unitOfWork,
    IConstraintViolationDetector constraintDetector,
    IValidator<CrearCargoRequest> crearValidator,
    IValidator<ActualizarCargoRequest> actualizarValidator) : ICargoServicioComandos
{
    /// <summary>
    /// Converts a PascalCase property name (e.g. <c>NivelId</c>) to camelCase
    /// (<c>nivelId</c>) so field-error keys match the JSON casing used by HTTP clients.
    /// </summary>
    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    /// <summary>
    /// Convenience constructor for backward compatibility (e.g., tests).
    /// Uses the real validators directly and a no-op constraint detector.
    /// </summary>
    public CargoServicioComandos(
        ICargoRepository repository,
        INivelCargoRepository nivelCargoRepository,
        IUnitOfWork unitOfWork)
        : this(repository, nivelCargoRepository, unitOfWork,
               NullConstraintViolationDetector.Instance,
               new CrearCargoRequestValidator(),
               new ActualizarCargoRequestValidator())
    {
    }

    public async Task<CargoCommandResult> CrearAsync(
        CrearCargoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var duplicate = await EnsureCodigoNoDuplicadoAsync(request.Codigo, excludingId: null, cancellationToken).ConfigureAwait(false);
        if (duplicate is not null) return duplicate;

        var nivel = await nivelCargoRepository.GetByIdAsync(request.NivelId, cancellationToken).ConfigureAwait(false);
        if (nivel is null)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Validation, "NivelCargoNoExiste",
                    "El nivel de cargo referenciado no existe."));
        }

        try
        {
            var cargo = new Cargo(request.Codigo, request.Nombre, request.NivelId, request.Descripcion)
            {
                Id = Guid.NewGuid()
            };

            await repository.AddAsync(cargo, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return CargoCommandResult.Success(MapToDto(cargo));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Conflict, "CodigoDuplicado",
                    "Ya existe un cargo activo con el mismo código."));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<CargoCommandResult> ActualizarAsync(
        Guid id,
        ActualizarCargoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await actualizarValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var cargo = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (cargo is null)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe."));
        }

        var duplicate = await EnsureCodigoNoDuplicadoAsync(request.Codigo, excludingId: id, cancellationToken).ConfigureAwait(false);
        if (duplicate is not null) return duplicate;

        var nivel = await nivelCargoRepository.GetByIdAsync(request.NivelId, cancellationToken).ConfigureAwait(false);
        if (nivel is null)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Validation, "NivelCargoNoExiste",
                    "El nivel de cargo referenciado no existe."));
        }

        try
        {
            cargo.Actualizar(request.Codigo, request.Nombre, request.NivelId, request.Descripcion);

            await repository.UpdateAsync(cargo, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return CargoCommandResult.Success(MapToDto(cargo));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Conflict, "CodigoDuplicado",
                    "Ya existe un cargo activo con el mismo código."));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<CargoCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cargo = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (cargo is null)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe."));
        }

        if (await repository.HasActivePuestosAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Conflict, "CargoConPuestosActivos",
                    "No se puede desactivar un cargo que tiene puestos activos asociados."));
        }

        try
        {
            cargo.Desactivar();
            await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return CargoCommandResult.Success(null!);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Validation, "DesactivacionInvalida", ex.Message));
        }
    }

    public async Task<CargoCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cargo = await repository.GetByIdIncludingDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        if (cargo is null)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe."));
        }

        if (await repository.ExistsActiveCodeAsync(cargo.Codigo, id, cancellationToken).ConfigureAwait(false))
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Conflict, "CodigoDuplicado",
                    "Ya existe un cargo activo con el mismo código."));
        }

        try
        {
            cargo.Activar();
            await repository.ReactivateAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return CargoCommandResult.Success(MapToDto(cargo));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Validation, "ReactivacionInvalida", ex.Message));
        }
    }

    private static CargoDto MapToDto(Cargo cargo)
    {
        return new CargoDto(
            cargo.Id,
            cargo.Codigo,
            cargo.Nombre,
            cargo.Descripcion,
            cargo.NivelId,
            cargo.NivelCargo?.Nombre);
    }

    /// <summary>
    /// Groups FluentValidation failures into a per-field dictionary using camelCase keys so the
    /// HTTP contract matches the JSON casing of incoming requests.
    /// </summary>
    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        return failures
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }

    /// <summary>
    /// Shared uniqueness check for <c>Codigo</c>. Returns a failure result when another
    /// active Cargo already uses the code (excluding the id when provided).
    /// </summary>
    private async Task<CargoCommandResult?> EnsureCodigoNoDuplicadoAsync(
        string codigo,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        if (await repository.ExistsActiveCodeAsync(codigo, excludingId, cancellationToken).ConfigureAwait(false))
        {
            return CargoCommandResult.Failure(
                new(CargoErrorType.Conflict, "CodigoDuplicado",
                    "Ya existe un cargo activo con el mismo código."));
        }
        return null;
    }
}

/// <summary>
/// No-op constraint violation detector used by the convenience constructor when no
/// detector is supplied (e.g., legacy test call sites). Always reports that the
/// exception is NOT a constraint violation, so the safety-net catch block is
/// effectively disabled in those test paths.
/// </summary>
internal sealed class NullConstraintViolationDetector : IConstraintViolationDetector
{
    public static readonly NullConstraintViolationDetector Instance = new();

    public bool IsConstraintViolation(DbUpdateException exception) => false;
}
