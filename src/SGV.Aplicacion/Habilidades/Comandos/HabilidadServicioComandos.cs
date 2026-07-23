using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Common;
using SGV.Aplicacion.Habilidades.Comandos.Validaciones;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Comandos;

/// <summary>
/// Implements create, update, soft-delete, and reactivate use cases for Habilidad.
///
/// <b>Breaking change (issue migrar-campo-categoria-habilidades-a-tabla):</b>
/// el campo legacy <c>string? Categoria</c> se reemplaza por
/// <c>Guid? CategoriaId</c>. La validación contra el catálogo se hace vía
/// <see cref="IHabilidadRepository.ExistsCategoriaAsync"/>; un id no
/// presente en el catálogo devuelve <c>HabilidadError.CategoriaInexistente</c>
/// con <c>Categoria = Validation</c> y HTTP <c>400 Bad Request</c>.
/// </summary>
public sealed class HabilidadServicioComandos(
    IHabilidadRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<CrearHabilidadRequest> crearValidator,
    IValidator<ActualizarHabilidadRequest> actualizarValidator) : IHabilidadServicioComandos
{
    /// <summary>
    /// Mensaje único para resultados de conflicto por <c>CodigoDuplicado</c>.
    /// </summary>
    private const string CodigoDuplicadoMessage = "Ya existe una habilidad activa con el mismo código.";

    /// <summary>
    /// Mensaje único para <c>CategoriaInexistente</c>.
    /// </summary>
    private const string CategoriaInexistenteMessage = "La categoría indicada no existe.";

    /// <summary>
    /// Nombre del índice activo de <c>Codigo</c> detectado en la
    /// <see cref="DbUpdateException"/> para mapear a <c>CodigoDuplicado</c>.
    /// Única fuente de verdad compartida entre el pre-check y la detección
    /// de la violación en <see cref="IsActiveCodigoUniqueViolation"/>.
    /// </summary>
    private const string ActiveCodigoUniqueIndex = "IX_Habilidades_ActiveCodigoUnique";

    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
        => ValidationHelper.BuildFieldErrors(failures);

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

        var duplicate = await EnsureCodigoNoDuplicadoAsync(request.Codigo, excludingId: null, cancellationToken).ConfigureAwait(false);
        if (duplicate is not null) return duplicate;

        if (request.CategoriaId.HasValue)
        {
            var existeCategoria = await repository.ExistsCategoriaAsync(request.CategoriaId.Value, cancellationToken).ConfigureAwait(false);
            if (!existeCategoria)
            {
                return FailureCategoriaInexistente();
            }
        }

        try
        {
            var habilidad = new Habilidad(request.Codigo, request.Nombre, request.CategoriaId, request.Descripcion)
            {
                Id = Guid.NewGuid()
            };

            await repository.AddAsync(habilidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return HabilidadCommandResult.Success(MapToDto(habilidad));
        }
        catch (DbUpdateException ex) when (IsActiveCodigoUniqueViolation(ex))
        {
            return FailureCodigoDuplicado();
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

        var duplicate = await EnsureCodigoNoDuplicadoAsync(request.Codigo, excludingId: id, cancellationToken).ConfigureAwait(false);
        if (duplicate is not null) return duplicate;

        if (request.CategoriaId.HasValue)
        {
            var existeCategoria = await repository.ExistsCategoriaAsync(request.CategoriaId.Value, cancellationToken).ConfigureAwait(false);
            if (!existeCategoria)
            {
                return FailureCategoriaInexistente();
            }
        }

        try
        {
            habilidad.Actualizar(request.Codigo, request.Nombre, request.CategoriaId, request.Descripcion);

            await repository.UpdateAsync(habilidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return HabilidadCommandResult.Success(MapToDto(habilidad));
        }
        catch (DbUpdateException ex) when (IsActiveCodigoUniqueViolation(ex))
        {
            return FailureCodigoDuplicado();
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
            return FailureCodigoDuplicado();
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
            habilidad.CategoriaId,
            habilidad.Categoria?.Nombre);
    }

    /// <summary>
    /// Shared uniqueness check for <c>Codigo</c>. Returns a failure result when
    /// another active Habilidad already uses the code (excluding the id when
    /// provided, so updating to the same code is a no-op rather than a
    /// conflict).
    /// </summary>
    private async Task<HabilidadCommandResult?> EnsureCodigoNoDuplicadoAsync(
        string codigo,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        if (await repository.ExistsActiveCodeAsync(codigo, excludingId, cancellationToken).ConfigureAwait(false))
        {
            return FailureCodigoDuplicado();
        }
        return null;
    }

    /// <summary>
    /// Factoría única del resultado de fallo <c>CodigoDuplicado</c>, usada
    /// tanto por el pre-check (<see cref="EnsureCodigoNoDuplicadoAsync"/>)
    /// como por la red de seguridad sobre <see cref="DbUpdateException"/> en
    /// <c>CrearAsync</c>/<c>ActualizarAsync</c>, y también en la ruta de
    /// reactivación. Centraliza el mensaje y el código de contrato HTTP.
    /// </summary>
    private static HabilidadCommandResult FailureCodigoDuplicado()
        => HabilidadCommandResult.Failure(
            new(HabilidadErrorType.Conflict, "CodigoDuplicado", CodigoDuplicadoMessage));

    /// <summary>
    /// Factoría única del resultado de fallo <c>CategoriaInexistente</c>
    /// (issue migrar-campo-categoria-habilidades-a-tabla). El código HTTP es
    /// <c>400 Bad Request</c> vía <see cref="ApiResults.ToProblemResult"/>
    /// porque <see cref="ErrorCategoriaMappers.ToCategoria"/> mapea
    /// <see cref="HabilidadErrorType.CategoriaInexistente"/> a
    /// <see cref="SGV.Contracts.Comun.ErrorCategoria.Validation"/>.
    /// </summary>
    private static HabilidadCommandResult FailureCategoriaInexistente()
        => HabilidadCommandResult.Failure(
            new(HabilidadErrorType.CategoriaInexistente, "CategoriaHabilidadNoExiste", CategoriaInexistenteMessage));

    /// <summary>
    /// Detects whether a <see cref="DbUpdateException"/> corresponds to a
    /// violation of the <see cref="ActiveCodigoUniqueIndex"/> index
    /// specifically. The check inspects the inner exception message for the
    /// MySQL "Duplicate entry ... for key" pattern referencing our
    /// active-codigo index. Any other constraint violation (FK, other unique
    /// indexes, check constraints) propagates as a generic 500 error
    /// instead of being misreported as <c>CodigoDuplicado</c>.
    /// </summary>
    /// <remarks>
    /// The match is done by inner message content (no MySqlException type
    /// reference) to keep <c>SGV.Aplicacion</c> free of any MySQL provider
    /// dependency (Clean Architecture). The combination "Duplicate entry" +
    /// the index name is MySQL-specific and is the exact message MySQL
    /// emits for violations of the active-codigo unique index.
    /// </remarks>
    private static bool IsActiveCodigoUniqueViolation(DbUpdateException exception)
    {
        var inner = exception.InnerException;
        if (inner is null)
        {
            return false;
        }

        var message = inner.Message;
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        return message.Contains(ActiveCodigoUniqueIndex, StringComparison.Ordinal)
            && (message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
                || message.Contains("1062", StringComparison.Ordinal));
    }
}