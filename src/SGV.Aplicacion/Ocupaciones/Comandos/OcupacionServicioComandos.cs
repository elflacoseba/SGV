using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Ocupaciones.Comandos.Validaciones;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Ocupaciones.Comandos;

/// <summary>
/// Application command service for Ocupacion write operations.
/// Orchestrates reference validation, uniqueness checks, domain methods, and persistence.
/// </summary>
public sealed class OcupacionServicioComandos : IOcupacionServicioComandos
{
    private readonly IOcupacionRepository ocupacionRepository;
    private readonly IPersonaRepository personaRepository;
    private readonly IPuestoRepository puestoRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IValidator<CrearOcupacionRequest> crearValidator;
    private readonly IValidator<ActualizarOcupacionRequest> actualizarValidator;
    private readonly IValidator<FinalizarOcupacionRequest> finalizarValidator;

    /// <summary>
    /// Primary constructor with full dependency set.
    /// </summary>
    public OcupacionServicioComandos(
        IOcupacionRepository ocupacionRepository,
        IPersonaRepository personaRepository,
        IPuestoRepository puestoRepository,
        IUnitOfWork unitOfWork,
        IValidator<CrearOcupacionRequest> crearValidator,
        IValidator<ActualizarOcupacionRequest> actualizarValidator,
        IValidator<FinalizarOcupacionRequest> finalizarValidator)
    {
        ArgumentNullException.ThrowIfNull(ocupacionRepository);
        ArgumentNullException.ThrowIfNull(personaRepository);
        ArgumentNullException.ThrowIfNull(puestoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(crearValidator);
        ArgumentNullException.ThrowIfNull(actualizarValidator);
        ArgumentNullException.ThrowIfNull(finalizarValidator);

        this.ocupacionRepository = ocupacionRepository;
        this.personaRepository = personaRepository;
        this.puestoRepository = puestoRepository;
        this.unitOfWork = unitOfWork;
        this.crearValidator = crearValidator;
        this.actualizarValidator = actualizarValidator;
        this.finalizarValidator = finalizarValidator;
    }

    /// <summary>
    /// Convenience constructor for tests and simple registration.
    /// Uses the real validators directly.
    /// </summary>
    public OcupacionServicioComandos(
        IOcupacionRepository ocupacionRepository,
        IPersonaRepository personaRepository,
        IPuestoRepository puestoRepository,
        IUnitOfWork unitOfWork)
        : this(ocupacionRepository, personaRepository, puestoRepository, unitOfWork,
               new CrearOcupacionRequestValidator(),
               new ActualizarOcupacionRequestValidator(),
               new FinalizarOcupacionRequestValidator())
    {
    }

    /// <summary>
    /// Converts a PascalCase property name to camelCase for field-error keys.
    /// </summary>
    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    // ── CrearAsync ─────────────────────────────────────────────

    public async Task<OcupacionCommandResult> CrearAsync(
        CrearOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var persona = await personaRepository.GetByIdIncludingDeletedAsync(request.PersonaId, cancellationToken).ConfigureAwait(false);
        if (persona is null)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.NotFound, "PersonaNoEncontrada", "La persona referenciada no existe."));
        }
        if (!persona.IsActive)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PersonaInactiva", "La persona referenciada no está activa."));
        }

        var puesto = await puestoRepository.GetByIdIncludingDeletedAsync(request.PuestoId, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.NotFound, "PuestoNoEncontrado", "El puesto referenciado no existe."));
        }
        if (!puesto.IsActive)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PuestoInactivo", "El puesto referenciado no está activo."));
        }

        // Issue 4: Check Persona+Puesto first (more specific), then Puesto alone.
        if (await ocupacionRepository.ExistsActiveByPersonaYPuestoAsync(request.PersonaId, request.PuestoId, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PersonaYPuestoOcupados", "Ya existe una ocupación activa para la misma persona y puesto."));
        }

        if (await ocupacionRepository.ExistsActiveByPuestoAsync(request.PuestoId, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PuestoOcupado", "Ya existe una ocupación activa para el puesto especificado."));
        }

        try
        {
            var ocupacion = new Ocupacion(
                request.PersonaId, request.PuestoId, request.FechaInicio,
                request.TipoAsignacion, observaciones: request.Observaciones)
            {
                Id = Guid.NewGuid()
            };

            await ocupacionRepository.AddAsync(ocupacion, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Issue 7: Direct access — validation guarantees non-null.
            var personaNombre = $"{persona.Nombres} {persona.Apellidos}";
            var puestoNombre = puesto.Nombre;
            return OcupacionCommandResult.Success(MapToDto(ocupacion, personaNombre, puestoNombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            // Issue 2: DbUpdateException caught for concurrent uniqueness violations.
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "DatosInvalidos", ex.Message));
        }
    }

    // ── ActualizarAsync ─────────────────────────────────────────

    public async Task<OcupacionCommandResult> ActualizarAsync(
        Guid id,
        ActualizarOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await actualizarValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var ocupacion = await ocupacionRepository.GetByIdIncludingHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        if (ocupacion is null)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada", "La ocupación no existe."));
        }

        if (!ocupacion.EsVigente)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "OcupacionNoEditable", "La ocupación no está activa y no se puede modificar."));
        }

        // Issue 1: Validation helpers return the loaded entity — no redundant fetch.
        var (personaError, persona) = await ValidarReferenciaPersonaAsync(
            request.PersonaId, nameof(request.PersonaId), cancellationToken).ConfigureAwait(false);
        if (personaError is not null) return personaError;

        var (puestoError, puesto) = await ValidarReferenciaPuestoAsync(
            request.PuestoId, nameof(request.PuestoId), cancellationToken).ConfigureAwait(false);
        if (puestoError is not null) return puestoError;

        if (await ocupacionRepository.ExistsActiveByPersonaYPuestoAsync(request.PersonaId, request.PuestoId, id, cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PersonaYPuestoOcupados", "Ya existe otra ocupación activa para la misma persona y puesto."));
        }

        if (await ocupacionRepository.ExistsActiveByPuestoAsync(request.PuestoId, id, cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PuestoOcupado", "Ya existe otra ocupación activa para el puesto especificado."));
        }

        try
        {
            ocupacion.Actualizar(request.PersonaId, request.PuestoId, request.FechaInicio, request.TipoAsignacion, request.Observaciones);

            await ocupacionRepository.UpdateAsync(ocupacion, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var personaNombre = $"{persona!.Nombres} {persona.Apellidos}";
            var puestoNombre = puesto!.Nombre;
            return OcupacionCommandResult.Success(MapToDto(ocupacion, personaNombre, puestoNombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            // Issue 2: DbUpdateException caught for concurrent uniqueness violations.
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "DatosInvalidos", ex.Message));
        }
    }

    // ── FinalizarAsync ──────────────────────────────────────────

    public async Task<OcupacionCommandResult> FinalizarAsync(
        Guid id,
        FinalizarOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await finalizarValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var ocupacion = await ocupacionRepository.GetByIdIncludingHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        if (ocupacion is null)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada", "La ocupación no existe."));
        }

        if (!ocupacion.EsVigente)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "OcupacionNoEditable", "La ocupación no está activa y no se puede finalizar."));
        }

        try
        {
            ocupacion.Finalizar(request.FechaFin, request.Observaciones);

            await ocupacionRepository.UpdateAsync(ocupacion, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Issue 3: Explicit fetch instead of relying on navigation properties.
            var persona = await personaRepository.GetByIdIncludingDeletedAsync(ocupacion.PersonaId, cancellationToken).ConfigureAwait(false);
            var puesto = await puestoRepository.GetByIdIncludingDeletedAsync(ocupacion.PuestoId, cancellationToken).ConfigureAwait(false);

            var personaNombre = persona is not null ? $"{persona.Nombres} {persona.Apellidos}" : "";
            var puestoNombre = puesto?.Nombre ?? "";

            return OcupacionCommandResult.Success(MapToDto(ocupacion, personaNombre, puestoNombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "FinalizacionInvalida", ex.Message));
        }
    }

    // ── EliminarAsync ───────────────────────────────────────────

    public async Task<OcupacionCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ocupacion = await ocupacionRepository.GetByIdIncludingHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        if (ocupacion is null)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada", "La ocupación no existe."));
        }

        if (!ocupacion.EsVigente)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "OcupacionNoEditable", "La ocupación no está activa y no se puede eliminar."));
        }

        try
        {
            ocupacion.EliminarLogicamente();

            await ocupacionRepository.UpdateAsync(ocupacion, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Issue 3: Explicit fetch instead of relying on navigation properties.
            var persona = await personaRepository.GetByIdIncludingDeletedAsync(ocupacion.PersonaId, cancellationToken).ConfigureAwait(false);
            var puesto = await puestoRepository.GetByIdIncludingDeletedAsync(ocupacion.PuestoId, cancellationToken).ConfigureAwait(false);

            var personaNombre = persona is not null ? $"{persona.Nombres} {persona.Apellidos}" : "";
            var puestoNombre = puesto?.Nombre ?? "";

            return OcupacionCommandResult.Success(MapToDto(ocupacion, personaNombre, puestoNombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "EliminacionInvalida", ex.Message));
        }
    }

    // ── ReactivarAsync ──────────────────────────────────────────

    public async Task<OcupacionCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ocupacion = await ocupacionRepository.GetByIdIncludingHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        if (ocupacion is null)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada", "La ocupación no existe."));
        }

        if (ocupacion.EsVigente)
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "OcupacionYaActiva", "La ocupación ya está activa."));
        }

        // Issue 1: Validation helpers return the loaded entity — no redundant fetch.
        var (personaError, persona) = await ValidarReferenciaPersonaAsync(
            ocupacion.PersonaId, nameof(ocupacion.PersonaId), cancellationToken).ConfigureAwait(false);
        if (personaError is not null) return personaError;

        var (puestoError, puesto) = await ValidarReferenciaPuestoAsync(
            ocupacion.PuestoId, nameof(ocupacion.PuestoId), cancellationToken).ConfigureAwait(false);
        if (puestoError is not null) return puestoError;

        if (await ocupacionRepository.ExistsActiveByPersonaYPuestoAsync(ocupacion.PersonaId, ocupacion.PuestoId, id, cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PersonaYPuestoOcupados", "Ya existe una ocupación activa para la misma persona y puesto."));
        }

        if (await ocupacionRepository.ExistsActiveByPuestoAsync(ocupacion.PuestoId, id, cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PuestoOcupado", "Ya existe una ocupación activa para el puesto especificado."));
        }

        try
        {
            ocupacion.Reactivar();

            await ocupacionRepository.UpdateAsync(ocupacion, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var personaNombre = $"{persona!.Nombres} {persona.Apellidos}";
            var puestoNombre = puesto!.Nombre;
            return OcupacionCommandResult.Success(MapToDto(ocupacion, personaNombre, puestoNombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            // Issue 5: Normalize to Conflict (was Validation).
            return OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "ReactivacionInvalida", ex.Message));
        }
    }

    // ── Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Validates persona reference and returns the loaded entity on success.
    /// </summary>
    private async Task<(OcupacionCommandResult? error, Persona? persona)> ValidarReferenciaPersonaAsync(
        Guid personaId, string fieldName, CancellationToken cancellationToken)
    {
        if (personaId == Guid.Empty)
        {
            var fieldErrors = new Dictionary<string, string[]>
            {
                [ToCamelCase(fieldName)] = ["La persona es obligatoria."]
            };
            return (OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Validation, "DatosInvalidos", "La persona no puede estar vacía."),
                fieldErrors), null);
        }

        var persona = await personaRepository.GetByIdIncludingDeletedAsync(personaId, cancellationToken).ConfigureAwait(false);
        if (persona is null)
        {
            return (OcupacionCommandResult.Failure(
                new(OcupacionErrorType.NotFound, "PersonaNoEncontrada", "La persona referenciada no existe.")), null);
        }
        if (!persona.IsActive)
        {
            return (OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PersonaInactiva", "La persona referenciada no está activa.")), null);
        }

        return (null, persona);
    }

    /// <summary>
    /// Validates puesto reference and returns the loaded entity on success.
    /// </summary>
    private async Task<(OcupacionCommandResult? error, Puesto? puesto)> ValidarReferenciaPuestoAsync(
        Guid puestoId, string fieldName, CancellationToken cancellationToken)
    {
        if (puestoId == Guid.Empty)
        {
            var fieldErrors = new Dictionary<string, string[]>
            {
                [ToCamelCase(fieldName)] = ["El puesto es obligatorio."]
            };
            return (OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Validation, "DatosInvalidos", "El puesto no puede estar vacío."),
                fieldErrors), null);
        }

        var puesto = await puestoRepository.GetByIdIncludingDeletedAsync(puestoId, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return (OcupacionCommandResult.Failure(
                new(OcupacionErrorType.NotFound, "PuestoNoEncontrado", "El puesto referenciado no existe.")), null);
        }
        if (!puesto.IsActive)
        {
            return (OcupacionCommandResult.Failure(
                new(OcupacionErrorType.Conflict, "PuestoInactivo", "El puesto referenciado no está activo.")), null);
        }

        return (null, puesto);
    }

    private static OcupacionDto MapToDto(Ocupacion ocupacion, string personaNombre, string puestoNombre)
    {
        return new OcupacionDto(
            ocupacion.Id,
            ocupacion.PersonaId,
            personaNombre,
            ocupacion.PuestoId,
            puestoNombre,
            ocupacion.FechaInicio,
            ocupacion.FechaFin,
            ocupacion.TipoAsignacion,
            ocupacion.Observaciones,
            OcupacionEstadoHelper.CalcularEstado(ocupacion));
    }

    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        return failures
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }
}
