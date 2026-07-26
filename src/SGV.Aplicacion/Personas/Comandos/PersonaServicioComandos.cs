using FluentValidation;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Common;
using SGV.Aplicacion.Personas.Comandos.Validaciones;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Seguridad;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Comandos;

/// <summary>
/// Implements create, update, soft-delete, and reactivate use cases for Persona,
/// with uniqueness checks for active Legajo, Email, and document.
/// </summary>
public sealed class PersonaServicioComandos(
    IPersonaRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<CrearPersonaRequest> crearValidator,
    IValidator<ActualizarPersonaRequest> actualizarValidator,
    IAuditoriaServicio auditoriaServicio,
    IUsuarioActual usuarioActual) : IPersonaServicioComandos
{
    /// <summary>
    /// Convenience constructor for backward compatibility (e.g., legacy
    /// tests que no necesitan explícitamente la auditoría ni el
    /// usuario actual). Usa los validators reales y un
    /// <see cref="NoopAuditoriaServicio"/> + un <see cref="NullUsuarioActual"/>
    /// para mantener el comportamiento previo a la issue #202.
    /// </summary>
    public PersonaServicioComandos(
        IPersonaRepository repository,
        IUnitOfWork unitOfWork)
        : this(repository, unitOfWork,
               new CrearPersonaRequestValidator(),
               new ActualizarPersonaRequestValidator(),
               new NoopAuditoriaServicio(),
               new NullUsuarioActual())
    {
    }

    public async Task<PersonaCommandResult> CrearAsync(
        CrearPersonaRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                ValidationHelper.BuildFieldErrors(validationResult.Errors));
        }

        var conflictError = await CheckUniquenessAsync(
            request.Legajo, request.Email, request.TipoDocumentoId, request.NumeroDocumento,
            null, cancellationToken).ConfigureAwait(false);
        if (conflictError is not null)
        {
            return PersonaCommandResult.Failure(conflictError);
        }

        try
        {
            var persona = new Persona(request.Nombres, request.Apellidos, request.Legajo, request.Email)
            {
                Id = Guid.NewGuid()
            };

            if (request.Telefono is not null)
            {
                persona.CambiarDatos(request.Nombres, request.Apellidos, request.Legajo, request.Email, request.Telefono);
            }

            if (request.TipoDocumentoId is not null || request.NumeroDocumento is not null)
            {
                persona.CambiarDocumento(request.TipoDocumentoId, request.NumeroDocumento);
            }

            await repository.AddAsync(persona, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PersonaCommandResult.Success(MapToDto(persona));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<PersonaCommandResult> ActualizarAsync(
        Guid id,
        ActualizarPersonaRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await actualizarValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                ValidationHelper.BuildFieldErrors(validationResult.Errors));
        }

        var persona = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (persona is null)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.NotFound, "PersonaNoEncontrada", "La persona no existe."));
        }

        var conflictError = await CheckUniquenessAsync(
            request.Legajo, request.Email, request.TipoDocumentoId, request.NumeroDocumento,
            id, cancellationToken).ConfigureAwait(false);
        if (conflictError is not null)
        {
            return PersonaCommandResult.Failure(conflictError);
        }

        try
        {
            // Issue #202: capturar el Legajo previo antes de aplicar el
            // cambio para detectar la transición no-nulo -> null y emitir
            // la fila de auditoría explícita correspondiente. El
            // interceptor central sigue emitiendo su fila Modificacion
            // genérica; ambas coexisten con Operation distinta y mismo
            // CorrelationId dentro de la misma unidad lógica.
            var legajoAnterior = persona.Legajo;

            persona.CambiarDatos(request.Nombres, request.Apellidos, request.Legajo, request.Email, request.Telefono);
            persona.CambiarDocumento(request.TipoDocumentoId, request.NumeroDocumento);

            await repository.UpdateAsync(persona, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (legajoAnterior is not null && persona.Legajo is null)
            {
                await auditoriaServicio.RegistrarAsync(
                    entidad: "Persona",
                    entityId: persona.Id.ToString(),
                    accion: "UpdateLegajo",
                    usuarioOperadorId: usuarioActual.UserId,
                    valoresAnteriores: new Dictionary<string, object?>
                    {
                        ["LegajoAnterior"] = legajoAnterior
                    },
                    valoresNuevos: new Dictionary<string, object?>
                    {
                        ["LegajoNuevo"] = null
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return PersonaCommandResult.Success(MapToDto(persona));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<PersonaCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var persona = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (persona is null)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.NotFound, "PersonaNoEncontrada", "La persona no existe."));
        }

        try
        {
            persona.Desactivar();
            await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PersonaCommandResult.Success(MapToDto(persona));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.Validation, "DesactivacionInvalida", ex.Message));
        }
    }

    public async Task<PersonaCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var persona = await repository.GetByIdIncludingDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        if (persona is null)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.NotFound, "PersonaNoEncontrada", "La persona no existe."));
        }

        // Check uniqueness for reactivation (excluding current persona)
        var legajo = persona.Legajo;
        var email = persona.Email;
        if (legajo is not null || email is not null ||
            persona.TipoDocumentoId is not null || persona.NumeroDocumento is not null)
        {
            var conflictError = await CheckUniquenessAsync(
                legajo, email, persona.TipoDocumentoId, persona.NumeroDocumento,
                id, cancellationToken).ConfigureAwait(false);
            if (conflictError is not null)
            {
                return PersonaCommandResult.Failure(conflictError);
            }
        }

        try
        {
            persona.Activar();
            await repository.ReactivateAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PersonaCommandResult.Success(MapToDto(persona));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PersonaCommandResult.Failure(
                new(PersonaErrorType.Validation, "ReactivacionInvalida", ex.Message));
        }
    }

    /// <summary>
    /// Checks uniqueness of legajo, email, and documento among active Personas.
    /// Returns a conflict error if any is duplicated, or null if all are unique.
    /// </summary>
    private async Task<PersonaError?> CheckUniquenessAsync(
        string? legajo, string? email,
        Guid? tipoDocumentoId, string? numeroDocumento,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(legajo) &&
            await repository.ExistsActiveLegajoAsync(legajo, excludingId, cancellationToken).ConfigureAwait(false))
        {
            return new(PersonaErrorType.Conflict, "LegajoDuplicado",
                "Ya existe una persona activa con el mismo legajo.");
        }

        if (!string.IsNullOrEmpty(email) &&
            await repository.ExistsActiveEmailAsync(email, excludingId, cancellationToken).ConfigureAwait(false))
        {
            return new(PersonaErrorType.Conflict, "EmailDuplicado",
                "Ya existe una persona activa con el mismo email.");
        }

        if (tipoDocumentoId.HasValue && !string.IsNullOrEmpty(numeroDocumento) &&
            await repository.ExistsActiveDocumentoAsync(tipoDocumentoId.Value, numeroDocumento, excludingId, cancellationToken)
                .ConfigureAwait(false))
        {
            return new(PersonaErrorType.Conflict, "DocumentoDuplicado",
                "Ya existe una persona activa con el mismo tipo y número de documento.");
        }

        return null;
    }

    private static PersonaDto MapToDto(Persona persona)
    {
        // Issue #147: TipoDocumentoCodigo/Nombre se proyectan en null en PR1.
        // El JOIN contra TiposDocumento (denormalización) entra en PR2 (T16
        // del tasks.md). Mantener los nombres para que el contrato del DTO
        // no rompa los call sites.
        return new PersonaDto(
            persona.Id,
            persona.Legajo,
            persona.Nombres,
            persona.Apellidos,
            persona.Email,
            persona.TipoDocumentoId,
            TipoDocumentoCodigo: null,
            TipoDocumentoNombre: null,
            persona.NumeroDocumento,
            persona.Telefono,
            persona.IsActive);
    }
}
