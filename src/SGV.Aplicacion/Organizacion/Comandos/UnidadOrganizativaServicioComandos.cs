using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Common;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Implements create, update, parent-change, and soft-delete use cases for organizational units.
/// </summary>
public sealed class UnidadOrganizativaServicioComandos(
    IUnidadOrganizativaRepository repository,
    ITipoUnidadOrganizativaRepository tipoUnidadRepository,
    IUnitOfWork unitOfWork,
    IConstraintViolationDetector constraintDetector,
    ILogger<UnidadOrganizativaServicioComandos> logger,
    IValidator<CrearUnidadOrganizativaRequest> crearValidator,
    IValidator<ActualizarUnidadOrganizativaRequest> actualizarValidator) : IUnidadOrganizativaServicioComandos
{
    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
        => ValidationHelper.BuildFieldErrors(failures);

    /// <summary>
    /// Convenience constructor for backward compatibility (e.g., tests).
    /// Uses the real validators directly.
    /// </summary>
    public UnidadOrganizativaServicioComandos(
        IUnidadOrganizativaRepository repository,
        ITipoUnidadOrganizativaRepository tipoUnidadRepository,
        IUnitOfWork unitOfWork,
        IConstraintViolationDetector constraintDetector)
        : this(repository, tipoUnidadRepository, unitOfWork,
               constraintDetector,
               Microsoft.Extensions.Logging.Abstractions.NullLogger<UnidadOrganizativaServicioComandos>.Instance,
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
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.DatosInvalidos, "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        if (await repository.ExistsActiveCodeAsync(request.Codigo, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.CodigoDuplicado, "Ya existe una unidad organizativa activa con el mismo código."));
        }

        var tipo = await tipoUnidadRepository.GetByIdAsync(request.TipoUnidadOrganizativaId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.TipoUnidadNoExiste,
                    "El tipo de unidad organizativa referenciado no existe."));
        }

        UnidadOrganizativa? padre = null;
        if (request.UnidadPadreId.HasValue)
        {
            padre = await repository.GetByIdAsync(request.UnidadPadreId.Value, cancellationToken).ConfigureAwait(false);
            if (padre is null)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.NotFound, UnidadOrganizativaErrorCodigos.UnidadPadreNoEncontrada, "La unidad padre especificada no existe."));
            }
        }

        try
        {
            var unidad = new UnidadOrganizativa(
                request.Codigo,
                request.Nombre,
                request.TipoUnidadOrganizativaId,
                request.Descripcion,
                request.UnidadPadreId)
            {
                Id = Guid.NewGuid()
            };
            unidad.DefinirVigencia(request.VigenteDesde, request.VigenteHasta);

            await repository.AddAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Issue #279: la instancia recién agregada no tiene las
            // navegaciones hidratadas, así que re-leemos con el método base
            // del repo (que carga TipoUnidadOrganizativa y UnidadPadre vía
            // Include) para devolver un DTO con tipoUnidadNombre,
            // unidadPadreCodigo y unidadPadreNombre correctos.
            var recargada = await repository.GetByIdAsync(unidad.Id, cancellationToken).ConfigureAwait(false);
            return UnidadOrganizativaCommandResult.Success(MapToDto(recargada ?? unidad));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            return MapConstraintViolation(ex, nameof(CrearAsync));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.DatosInvalidos, ex.Message));
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
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.DatosInvalidos, "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var unidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, UnidadOrganizativaErrorCodigos.UnidadNoEncontrada, "La unidad organizativa no existe."));
        }

        var tipo = await tipoUnidadRepository.GetByIdAsync(request.TipoUnidadOrganizativaId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.TipoUnidadNoExiste,
                    "El tipo de unidad organizativa referenciado no existe."));
        }

        // Issue #277 (WU-2): PUT previously skipped padre integrity entirely,
        // letting padre-inexistente or padre-descendiente slip through and
        // poisoning the hierarchy. Mirror the validation block already used
        // by CambiarUnidadPadreAsync: existence -> 404; cycle -> 409.
        // Self-reference is still caught by Dominio.Actualizar via
        // `InvalidOperationException("...padre de sí misma")` inside the
        // try below (translates to "DatosInvalidos"). See spec
        // unidad-organizativa-crud "PUT valida integridad del padre".
        if (request.UnidadPadreId.HasValue)
        {
            var padre = await repository.GetByIdAsync(request.UnidadPadreId.Value, cancellationToken).ConfigureAwait(false);
            if (padre is null)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.NotFound, UnidadOrganizativaErrorCodigos.UnidadPadreNoEncontrada,
                        "La unidad padre especificada no existe."));
            }

            try
            {
                if (await repository.IsDescendantAsync(request.UnidadPadreId.Value, id, cancellationToken).ConfigureAwait(false))
                {
                    return UnidadOrganizativaCommandResult.Failure(
                        new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.CicloJerarquico,
                            "No se puede asignar como padre una unidad descendiente."));
                }
            }
            catch (InvalidOperationException ex) when (ex.Message == UnidadOrganizativaErrorCodigos.CicloJerarquico)
            {
                // Pre-existing cycle in BD would otherwise cause an
                // infinite loop in IsDescendantAsync; the repository
                // raises the canonical code which we translate to 409.
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.CicloJerarquico,
                        "No se puede asignar como padre una unidad descendiente."));
            }
        }

        try
        {
            // El codigo se preserva por contrato: Actualizar no acepta codigo
            // y el record se muta via private set, manteniendo el Codigo
            // original intacto.
            unidad.Actualizar(
                request.Nombre,
                request.Descripcion,
                request.TipoUnidadOrganizativaId,
                request.UnidadPadreId,
                request.VigenteDesde,
                request.VigenteHasta);

            await repository.UpdateAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Issue #279: la navegación TipoUnidadOrganizativa cargada por
            // GetByIdForUpdateAsync queda stale si el request cambió el
            // tipo, y UnidadPadre nunca se cargó. Re-leemos con el método
            // base del repo para devolver un DTO con ambas navegaciones
            // frescas (incluyendo el padre nuevo cuando aplique).
            var recargada = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return UnidadOrganizativaCommandResult.Success(MapToDto(recargada ?? unidad));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            return MapConstraintViolation(ex, nameof(ActualizarAsync));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.DatosInvalidos, ex.Message));
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
                new(UnidadOrganizativaErrorType.NotFound, UnidadOrganizativaErrorCodigos.UnidadNoEncontrada, "La unidad organizativa no existe."));
        }

        if (request.UnidadPadreId == id)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.CicloJerarquico, "Una unidad organizativa no puede ser padre de sí misma."));
        }

        UnidadOrganizativa? nuevoPadre = null;
        if (request.UnidadPadreId.HasValue)
        {
            nuevoPadre = await repository.GetByIdAsync(request.UnidadPadreId.Value, cancellationToken).ConfigureAwait(false);
            if (nuevoPadre is null)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.NotFound, UnidadOrganizativaErrorCodigos.UnidadPadreNoEncontrada, "La unidad padre especificada no existe."));
            }

            // Issue #277 (housekeeping W-A1/R-A1): simetría con ActualizarAsync.
            // Si la BD arrastra un ciclo pre-existente, IsDescendantAsync lanza
            // InvalidOperationException("CicloJerarquico") para cortar su propio
            // bucle. Sin este catch, la excepción escapa del servicio y el
            // PATCH /unidad-padre responde 500 en lugar del 409 esperado.
            try
            {
                if (await repository.IsDescendantAsync(request.UnidadPadreId.Value, id, cancellationToken).ConfigureAwait(false))
                {
                    return UnidadOrganizativaCommandResult.Failure(
                        new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.CicloJerarquico, "No se puede asignar como padre una unidad descendiente."));
                }
            }
            catch (InvalidOperationException ex) when (ex.Message == UnidadOrganizativaErrorCodigos.CicloJerarquico)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.CicloJerarquico,
                        "No se puede asignar como padre una unidad descendiente."));
            }
        }

        try
        {
            unidad.CambiarUnidadPadre(request.UnidadPadreId);

            await repository.UpdateAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Issue #279: GetByIdForUpdateAsync sólo carga TipoUnidadOrganizativa,
            // nunca UnidadPadre. Re-leemos para que el DTO de respuesta traiga
            // unidadPadreCodigo y unidadPadreNombre del nuevo padre.
            var recargada = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return UnidadOrganizativaCommandResult.Success(MapToDto(recargada ?? unidad));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            return MapConstraintViolation(ex, nameof(CambiarUnidadPadreAsync));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.CicloJerarquico, ex.Message));
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
                new(UnidadOrganizativaErrorType.NotFound, UnidadOrganizativaErrorCodigos.UnidadNoEncontrada, "La unidad organizativa no existe."));
        }

        if (await repository.HasActiveChildrenAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.UnidadConHijasActivas,
                    "No se puede eliminar una unidad organizativa que tiene hijas activas."));
        }

        if (await repository.HasActivePuestosAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.UnidadConPuestosActivos,
                    "No se puede eliminar una unidad organizativa que tiene puestos activos asociados."));
        }

        unidad.Desactivar();
        await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            return MapConstraintViolation(ex, nameof(EliminarAsync));
        }

        return UnidadOrganizativaCommandResult.Success(null!);
    }

    public async Task<UnidadOrganizativaCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unidad = await repository.GetByIdIncludingDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, UnidadOrganizativaErrorCodigos.UnidadNoEncontrada, "La unidad organizativa no existe."));
        }

        if (await repository.ExistsActiveCodeAsync(unidad.Codigo, id, cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.CodigoDuplicado,
                    "Ya existe una unidad organizativa activa con el mismo código."));
        }

        // Check that the parent (if any) is active before reactivating
        if (unidad.UnidadPadreId.HasValue)
        {
            var padre = await repository.GetByIdIncludingDeletedAsync(unidad.UnidadPadreId.Value, cancellationToken).ConfigureAwait(false);
            if (padre is null || !padre.IsActive)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.PadreInactivo,
                        "No se puede reactivar una unidad organizativa cuyo padre está inactivo o eliminado."));
            }
        }

        try
        {
            unidad.Activar();

            await repository.ReactivateAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Issue #279: GetByIdIncludingDeletedAsync sólo carga TipoUnidadOrganizativa.
            // Tras reactivar, la unidad vuelve a estar activa, así que usamos el
            // método base del repo para devolver un DTO con ambas navegaciones
            // frescas.
            var recargada = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return UnidadOrganizativaCommandResult.Success(MapToDto(recargada ?? unidad));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            return MapConstraintViolation(ex, nameof(ReactivarAsync));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, UnidadOrganizativaErrorCodigos.ReactivacionInvalida, ex.Message));
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
            unidad.UnidadPadreId,
            unidad.UnidadPadre?.Codigo,
            unidad.UnidadPadre?.Nombre);
    }

    /// <summary>
    /// H-A3 (housekeeping release-readiness UO+Organigrama): traduce
    /// <see cref="DbUpdateException"/> a un resultado de negocio tipado.
    /// Cubre el SIGNAL 1644 del trigger anti-ciclos (issue #277) y la
    /// violación del índice único <c>IX_UnidadesOrganizativas_ActiveCodigoUnique</c>
    /// en la carrera entre <c>ExistsActiveCodeAsync</c> y <c>SaveChanges</c>.
    /// El contrato del trigger emite <c>MESSAGE_TEXT = 'CicloJerarquico'</c>
    /// (migración <c>20260816203122_AddTriggerAntiCiclosUnidadesOrganizativas</c>),
    /// así que la distinción se hace por el mensaje del InnerException y por
    /// <see cref="IConstraintViolationDetector.GetUniqueConstraintName"/> sin
    /// tener que importar <c>MySqlConnector</c> en la capa de aplicación.
    /// </summary>
    private UnidadOrganizativaCommandResult MapConstraintViolation(
        DbUpdateException ex, string methodName)
    {
        logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", methodName, ex.Message);

        // 1644 (SIGNAL del trigger anti-ciclos): el InnerException trae el
        // MESSAGE_TEXT literal del SIGNAL, que la migración garantiza como
        // "CicloJerarquico" en una constante.
        if (ex.InnerException is not null &&
            ex.InnerException.Message.Contains(UnidadOrganizativaErrorCodigos.CicloJerarquico, StringComparison.Ordinal))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.CicloJerarquico,
                    "No se puede asignar como padre una unidad descendiente."));
        }

        // 1062 (duplicate key) sobre el índice único del código activo.
        // Carrera entre ExistsActiveCodeAsync y SaveChangesAsync: el chequeo
        // previo pasa pero la BD rechaza la segunda escritura concurrente.
        var uniqueConstraintName = constraintDetector.GetUniqueConstraintName(ex);
        if (uniqueConstraintName == "IX_UnidadesOrganizativas_ActiveCodigoUnique")
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.CodigoDuplicado,
                    "Ya existe una unidad organizativa activa con el mismo código."));
        }

        // Otros constraint violations (FK 1169/1451/1452, 4025, etc): 409
        // genérico para no exponer detalles de BD al cliente.
        return UnidadOrganizativaCommandResult.Failure(
            new(UnidadOrganizativaErrorType.Conflict, UnidadOrganizativaErrorCodigos.RestriccionDeIntegridad,
                "La operación viola una restricción de integridad."));
    }

}
