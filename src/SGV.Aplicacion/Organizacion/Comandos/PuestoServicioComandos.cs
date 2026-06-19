using FluentValidation;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Implements create, update, soft-delete, and reactivate use cases for Puesto.
/// </summary>
public sealed class PuestoServicioComandos(
    IPuestoRepository repository,
    IUnidadOrganizativaRepository unidadOrganizativaRepository,
    ICargoRepository cargoRepository,
    IUnitOfWork unitOfWork,
    IValidator<CrearPuestoRequest> crearValidator,
    IValidator<ActualizarPuestoRequest> actualizarValidator) : IPuestoServicioComandos
{
    /// <summary>
    /// Converts a PascalCase property name to camelCase for field-error keys.
    /// </summary>
    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    /// <summary>
    /// Convenience constructor for backward compatibility (e.g., tests).
    /// Uses the real validators directly.
    /// </summary>
    public PuestoServicioComandos(
        IPuestoRepository repository,
        IUnidadOrganizativaRepository unidadOrganizativaRepository,
        ICargoRepository cargoRepository,
        IUnitOfWork unitOfWork)
        : this(repository, unidadOrganizativaRepository, cargoRepository, unitOfWork,
               new CrearPuestoRequestValidator(),
               new ActualizarPuestoRequestValidator())
    {
    }

    public async Task<PuestoCommandResult> CrearAsync(
        CrearPuestoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        if (await repository.ExistsActiveCodeAsync(request.Codigo, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Conflict, "CodigoDuplicado", "Ya existe un puesto activo con el mismo código."));
        }

        var unidad = await unidadOrganizativaRepository
            .GetByIdAsync(request.UnidadOrganizativaId, cancellationToken)
            .ConfigureAwait(false);
        if (unidad is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "UnidadOrganizativaNoExiste",
                    "La unidad organizativa referenciada no existe."));
        }

        var cargo = await cargoRepository
            .GetByIdAsync(request.CargoId, cancellationToken)
            .ConfigureAwait(false);
        if (cargo is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "CargoNoExiste",
                    "El cargo referenciado no existe."));
        }

        if (request.PuestoSuperiorId.HasValue)
        {
            var superiorError = await ValidarPuestoSuperiorAsync(
                request.PuestoSuperiorId.Value, cancellationToken).ConfigureAwait(false);
            if (superiorError is not null)
            {
                return superiorError;
            }
        }

        try
        {
            var puesto = new Puesto(
                request.UnidadOrganizativaId,
                request.CargoId,
                request.Codigo,
                request.Nombre,
                request.PuestoSuperiorId,
                request.Descripcion)
            {
                Id = Guid.NewGuid()
            };

            await repository.AddAsync(puesto, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PuestoCommandResult.Success(MapToDto(puesto, unidad.Nombre, cargo.Nombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<PuestoCommandResult> ActualizarAsync(
        Guid id,
        ActualizarPuestoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await actualizarValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var puesto = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe."));
        }

        if (request.PuestoSuperiorId.HasValue)
        {
            if (request.PuestoSuperiorId.Value == id)
            {
                return PuestoCommandResult.Failure(
                    new(PuestoErrorType.Validation, "PuestoSuperiorInvalido",
                        "Un puesto no puede ser superior de sí mismo."));
            }

            var superiorError = await ValidarPuestoSuperiorAsync(
                request.PuestoSuperiorId.Value, cancellationToken).ConfigureAwait(false);
            if (superiorError is not null)
            {
                return superiorError;
            }
        }

        try
        {
            puesto.Actualizar(request.Nombre, request.Descripcion, request.PuestoSuperiorId);

            await repository.UpdateAsync(puesto, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PuestoCommandResult.Success(MapToDto(puesto, puesto.UnidadOrganizativa?.Nombre, puesto.Cargo?.Nombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<PuestoCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var puesto = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe."));
        }

        try
        {
            puesto.Desactivar();
            await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PuestoCommandResult.Success(null!);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DesactivacionInvalida", ex.Message));
        }
    }

    public async Task<PuestoCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var puesto = await repository.GetByIdIncludingDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe."));
        }

        if (await repository.ExistsActiveCodeAsync(puesto.Codigo, id, cancellationToken).ConfigureAwait(false))
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Conflict, "CodigoDuplicado",
                    "Ya existe un puesto activo con el mismo código."));
        }

        try
        {
            puesto.Activar();
            await repository.ReactivateAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PuestoCommandResult.Success(MapToDto(puesto, puesto.UnidadOrganizativa?.Nombre, puesto.Cargo?.Nombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "ReactivacionInvalida", ex.Message));
        }
    }

    private async Task<PuestoCommandResult?> ValidarPuestoSuperiorAsync(
        Guid puestoSuperiorId,
        CancellationToken cancellationToken)
    {
        var superior = await repository
            .GetByIdAsync(puestoSuperiorId, cancellationToken)
            .ConfigureAwait(false);

        if (superior is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "PuestoSuperiorNoExiste",
                    "El puesto superior referenciado no existe o no está activo."));
        }

        return null;
    }

    private static PuestoDto MapToDto(Puesto puesto, string? unidadNombre, string? cargoNombre)
    {
        return new PuestoDto(
            puesto.Id,
            puesto.Codigo,
            puesto.Nombre,
            puesto.Descripcion,
            puesto.UnidadOrganizativaId,
            unidadNombre ?? string.Empty,
            puesto.CargoId,
            cargoNombre ?? string.Empty,
            puesto.PuestoSuperiorId);
    }

    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        return failures
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }
}
