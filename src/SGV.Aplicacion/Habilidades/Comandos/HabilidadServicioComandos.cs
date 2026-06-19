using FluentValidation;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Comandos.Validaciones;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Comandos;

/// <summary>
/// Implements create, update, soft-delete, and reactivate use cases for Habilidad.
/// </summary>
public sealed class HabilidadServicioComandos(
    IHabilidadRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<CrearHabilidadRequest> crearValidator,
    IValidator<ActualizarHabilidadRequest> actualizarValidator) : IHabilidadServicioComandos
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
    public HabilidadServicioComandos(
        IHabilidadRepository repository,
        IUnitOfWork unitOfWork)
        : this(repository, unitOfWork,
               new CrearHabilidadRequestValidator(),
               new ActualizarHabilidadRequestValidator())
    {
    }

    public async Task<HabilidadCommandResult> CrearAsync(
        CrearHabilidadRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        if (await repository.ExistsActiveCodeAsync(request.Codigo, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.Conflict, "CodigoDuplicado", "Ya existe una habilidad activa con el mismo código."));
        }

        try
        {
            var habilidad = new Habilidad(request.Codigo, request.Nombre, request.Categoria, request.Descripcion)
            {
                Id = Guid.NewGuid()
            };

            await repository.AddAsync(habilidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return HabilidadCommandResult.Success(MapToDto(habilidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<HabilidadCommandResult> ActualizarAsync(
        Guid id,
        ActualizarHabilidadRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await actualizarValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var habilidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (habilidad is null)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.NotFound, "HabilidadNoEncontrada", "La habilidad no existe."));
        }

        try
        {
            habilidad.Actualizar(request.Nombre, request.Categoria, request.Descripcion);

            await repository.UpdateAsync(habilidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return HabilidadCommandResult.Success(MapToDto(habilidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<HabilidadCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var habilidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (habilidad is null)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.NotFound, "HabilidadNoEncontrada", "La habilidad no existe."));
        }

        try
        {
            habilidad.Desactivar();
            await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return HabilidadCommandResult.Success(MapToDto(habilidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.Validation, "DesactivacionInvalida", ex.Message));
        }
    }

    public async Task<HabilidadCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var habilidad = await repository.GetByIdIncludingDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        if (habilidad is null)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.NotFound, "HabilidadNoEncontrada", "La habilidad no existe."));
        }

        if (await repository.ExistsActiveCodeAsync(habilidad.Codigo, id, cancellationToken).ConfigureAwait(false))
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.Conflict, "CodigoDuplicado",
                    "Ya existe una habilidad activa con el mismo código."));
        }

        try
        {
            habilidad.Activar();
            await repository.ReactivateAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return HabilidadCommandResult.Success(MapToDto(habilidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return HabilidadCommandResult.Failure(
                new(HabilidadErrorType.Validation, "ReactivacionInvalida", ex.Message));
        }
    }

    private static HabilidadDto MapToDto(Habilidad habilidad)
    {
        return new HabilidadDto(
            habilidad.Id,
            habilidad.Codigo,
            habilidad.Nombre,
            habilidad.Descripcion,
            habilidad.Categoria);
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
}
