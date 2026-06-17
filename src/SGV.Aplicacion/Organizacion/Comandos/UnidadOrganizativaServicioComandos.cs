using FluentValidation;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Implements create, update, parent-change, and soft-delete use cases for organizational units.
/// </summary>
public sealed class UnidadOrganizativaServicioComandos(
    IUnidadOrganizativaRepository repository,
    ITipoUnidadOrganizativaRepository tipoUnidadRepository,
    IUnitOfWork unitOfWork,
    IValidator<CrearUnidadOrganizativaRequest> crearValidator,
    IValidator<ActualizarUnidadOrganizativaRequest> actualizarValidator) : IUnidadOrganizativaServicioComandos
{
    /// <summary>
    /// Converts a PascalCase property name (e.g. <c>TipoUnidadOrganizativaId</c>) to camelCase
    /// (<c>tipoUnidadOrganizativaId</c>) so field-error keys match the JSON casing used by HTTP clients.
    /// </summary>
    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    /// <summary>
    /// Convenience constructor for backward compatibility (e.g., tests).
    /// Uses the real validators directly.
    /// </summary>
    public UnidadOrganizativaServicioComandos(
        IUnidadOrganizativaRepository repository,
        ITipoUnidadOrganizativaRepository tipoUnidadRepository,
        IUnitOfWork unitOfWork)
        : this(repository, tipoUnidadRepository, unitOfWork,
               new CrearUnidadOrganizativaRequestValidator(),
               new ActualizarUnidadOrganizativaRequestValidator())
    {
    }
    public async Task<UnidadOrganizativaCommandResult> CrearAsync(
        CrearUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        if (await repository.ExistsActiveCodeAsync(request.Codigo, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado", "Ya existe una unidad organizativa activa con el mismo código."));
        }

        var tipo = await tipoUnidadRepository.GetByIdAsync(request.TipoUnidadOrganizativaId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "TipoUnidadNoExiste",
                    "El tipo de unidad organizativa referenciado no existe."));
        }

        if (request.UnidadPadreId.HasValue)
        {
            var padre = await repository.GetByIdAsync(request.UnidadPadreId.Value, cancellationToken).ConfigureAwait(false);
            if (padre is null)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.NotFound, "UnidadPadreNoEncontrada", "La unidad padre especificada no existe."));
            }
        }

        try
        {
            var unidad = new UnidadOrganizativa(request.Codigo, request.Nombre, request.TipoUnidadOrganizativaId, request.UnidadPadreId)
            {
                Id = Guid.NewGuid()
            };
            unidad.CambiarDatos(request.Codigo, request.Nombre, request.TipoUnidadOrganizativaId, request.Descripcion);
            unidad.DefinirVigencia(request.VigenteDesde, request.VigenteHasta);

            await repository.AddAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return UnidadOrganizativaCommandResult.Success(MapToDto(unidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<UnidadOrganizativaCommandResult> ActualizarAsync(
        Guid id,
        ActualizarUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await actualizarValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var unidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad organizativa no existe."));
        }

        if (await repository.ExistsActiveCodeAsync(request.Codigo, id, cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado", "Ya existe una unidad organizativa activa con el mismo código."));
        }

        var tipo = await tipoUnidadRepository.GetByIdAsync(request.TipoUnidadOrganizativaId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "TipoUnidadNoExiste",
                    "El tipo de unidad organizativa referenciado no existe."));
        }

        try
        {
            unidad.CambiarDatos(request.Codigo, request.Nombre, request.TipoUnidadOrganizativaId, request.Descripcion);
            unidad.DefinirVigencia(request.VigenteDesde, request.VigenteHasta);

            await repository.UpdateAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return UnidadOrganizativaCommandResult.Success(MapToDto(unidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<UnidadOrganizativaCommandResult> CambiarUnidadPadreAsync(
        Guid id,
        CambiarUnidadPadreRequest request,
        CancellationToken cancellationToken = default)
    {
        var unidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad organizativa no existe."));
        }

        if (request.UnidadPadreId == id)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "CicloJerarquico", "Una unidad organizativa no puede ser padre de sí misma."));
        }

        if (request.UnidadPadreId.HasValue)
        {
            var padre = await repository.GetByIdAsync(request.UnidadPadreId.Value, cancellationToken).ConfigureAwait(false);
            if (padre is null)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.NotFound, "UnidadPadreNoEncontrada", "La unidad padre especificada no existe."));
            }

            if (await repository.IsDescendantAsync(request.UnidadPadreId.Value, id, cancellationToken).ConfigureAwait(false))
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.Conflict, "CicloJerarquico", "No se puede asignar como padre una unidad descendiente."));
            }
        }

        try
        {
            unidad.CambiarUnidadPadre(request.UnidadPadreId);

            await repository.UpdateAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return UnidadOrganizativaCommandResult.Success(MapToDto(unidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "CicloJerarquico", ex.Message));
        }
    }

    public async Task<UnidadOrganizativaCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var unidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad organizativa no existe."));
        }

        unidad.Desactivar();
        await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UnidadOrganizativaCommandResult.Success(null!);
    }

    public async Task<UnidadOrganizativaCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
         var unidad = await repository.GetByIdIncludingDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad organizativa no existe."));
        }

        if (await repository.ExistsActiveCodeAsync(unidad.Codigo, id, cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado",
                    "Ya existe una unidad organizativa activa con el mismo código."));
        }

        try
        {
            unidad.Activar();

            await repository.ReactivateAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return UnidadOrganizativaCommandResult.Success(MapToDto(unidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "ReactivacionInvalida", ex.Message));
        }
    }

    private static UnidadOrganizativaDto MapToDto(UnidadOrganizativa unidad)
    {
        return new UnidadOrganizativaDto(
            unidad.Id,
            unidad.Codigo,
            unidad.Nombre,
            unidad.TipoUnidadOrganizativaId,
            unidad.TipoUnidadOrganizativa?.Nombre ?? string.Empty,
            unidad.Descripcion,
            unidad.VigenteDesde,
            unidad.VigenteHasta,
            unidad.UnidadPadreId);
    }

    /// <summary>
    /// Groups FluentValidation failures into a per-field dictionary using camelCase keys so the
    /// HTTP contract (<c>errors[codigo]</c>, <c>errors[nombre]</c>, <c>errors[tipoUnidadOrganizativaId]</c>)
    /// matches the JSON casing of incoming requests.
    /// </summary>
    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        return failures
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }

    
}
