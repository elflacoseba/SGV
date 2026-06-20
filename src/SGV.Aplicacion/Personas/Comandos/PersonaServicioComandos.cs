using FluentValidation;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Personas.Comandos.Validaciones;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;
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
    IValidator<ActualizarPersonaRequest> actualizarValidator) : IPersonaServicioComandos
{
    /// <summary>
    /// Convenience constructor for backward compatibility (e.g., tests).
    /// Uses the real validators directly.
    /// </summary>
    public PersonaServicioComandos(
        IPersonaRepository repository,
        IUnitOfWork unitOfWork)
        : this(repository, unitOfWork,
               new CrearPersonaRequestValidator(),
               new ActualizarPersonaRequestValidator())
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
            request.Legajo, request.Email, request.TipoDocumento, request.NumeroDocumento,
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

            if (request.TipoDocumento is not null || request.NumeroDocumento is not null)
            {
                persona.CambiarDocumento(request.TipoDocumento, request.NumeroDocumento);
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
            request.Legajo, request.Email, request.TipoDocumento, request.NumeroDocumento,
            id, cancellationToken).ConfigureAwait(false);
        if (conflictError is not null)
        {
            return PersonaCommandResult.Failure(conflictError);
        }

        try
        {
            persona.CambiarDatos(request.Nombres, request.Apellidos, request.Legajo, request.Email, request.Telefono);
            persona.CambiarDocumento(request.TipoDocumento, request.NumeroDocumento);

            await repository.UpdateAsync(persona, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
            persona.TipoDocumento is not null || persona.NumeroDocumento is not null)
        {
            var conflictError = await CheckUniquenessAsync(
                legajo, email, persona.TipoDocumento, persona.NumeroDocumento,
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
        string? tipoDocumento, string? numeroDocumento,
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

        if (!string.IsNullOrEmpty(tipoDocumento) && !string.IsNullOrEmpty(numeroDocumento) &&
            await repository.ExistsActiveDocumentoAsync(tipoDocumento, numeroDocumento, excludingId, cancellationToken)
                .ConfigureAwait(false))
        {
            return new(PersonaErrorType.Conflict, "DocumentoDuplicado",
                "Ya existe una persona activa con el mismo tipo y número de documento.");
        }

        return null;
    }

    private static PersonaDto MapToDto(Persona persona)
    {
        return new PersonaDto(
            persona.Id,
            persona.Legajo,
            persona.Nombres,
            persona.Apellidos,
            persona.Email,
            persona.TipoDocumento,
            persona.NumeroDocumento,
            persona.Telefono,
            persona.IsActive);
    }
}
