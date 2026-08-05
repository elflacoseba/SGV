using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Common;
using SGV.Aplicacion.Ocupaciones.Comandos.Validaciones;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
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
    private readonly IVacanteRepository vacanteRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IConstraintViolationDetector constraintDetector;
    private readonly ILogger<OcupacionServicioComandos> logger;
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
        IConstraintViolationDetector constraintDetector,
        ILogger<OcupacionServicioComandos> logger,
        IValidator<CrearOcupacionRequest> crearValidator,
        IValidator<ActualizarOcupacionRequest> actualizarValidator,
        IValidator<FinalizarOcupacionRequest> finalizarValidator,
        IVacanteRepository vacanteRepository)
    {
        ArgumentNullException.ThrowIfNull(ocupacionRepository);
        ArgumentNullException.ThrowIfNull(personaRepository);
        ArgumentNullException.ThrowIfNull(puestoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(constraintDetector);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(crearValidator);
        ArgumentNullException.ThrowIfNull(actualizarValidator);
        ArgumentNullException.ThrowIfNull(finalizarValidator);
        ArgumentNullException.ThrowIfNull(vacanteRepository);

        this.ocupacionRepository = ocupacionRepository;
        this.personaRepository = personaRepository;
        this.puestoRepository = puestoRepository;
        this.vacanteRepository = vacanteRepository;
        this.unitOfWork = unitOfWork;
        this.constraintDetector = constraintDetector;
        this.logger = logger;
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
        IUnitOfWork unitOfWork,
        IConstraintViolationDetector constraintDetector,
        ILogger<OcupacionServicioComandos> logger,
        IVacanteRepository vacanteRepository)
        : this(ocupacionRepository, personaRepository, puestoRepository, unitOfWork,
               constraintDetector, logger,
               new CrearOcupacionRequestValidator(),
               new ActualizarOcupacionRequestValidator(),
               new FinalizarOcupacionRequestValidator(),
               vacanteRepository)
    {
    }

    /// <summary>
    /// Groups FluentValidation failures into a per-field dictionary using
    /// camelCase keys so the HTTP contract matches the JSON casing of
    /// incoming requests.
    /// </summary>
    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
        => ValidationHelper.BuildFieldErrors(failures);

    /// <summary>
    /// Helper used to convert a property name to camelCase for inline
    /// FieldErrors dictionaries produced by manual reference validators
    /// (e.g. when the request omits a required id). Delegates to the
    /// shared helper to avoid drift.
    /// </summary>
    private static string ToCamelCase(string propertyName)
        => ValidationHelper.ToCamelCase(propertyName);

    // ── CrearAsync ─────────────────────────────────────────────

    public async Task<OcupacionCommandResult> CrearAsync(
        CrearOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var persona = await personaRepository.GetByIdIncludingDeletedAsync(request.PersonaId, cancellationToken).ConfigureAwait(false);
        if (persona is null)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.NotFound, "PersonaNoEncontrada", "La persona referenciada no existe."));
        }
        if (!persona.IsActive)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "PersonaInactiva", "La persona referenciada no está activa."));
        }

        var puesto = await puestoRepository.GetByIdIncludingDeletedAsync(request.PuestoId, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.NotFound, "PuestoNoEncontrado", "El puesto referenciado no existe."));
        }
        if (!puesto.IsActive)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "PuestoInactivo", "El puesto referenciado no está activo."));
        }

        // N3 (change vacante-ocupacion-flow-alignment): el alta directa de
        // Ocupacion requiere una Vacante abierta para el mismo Puesto. Sin
        // ella, el camino normal es Cubrir la Vacante (N2). El flujo principal
        // (REQ-OCC-FORM-009 + REQ-OCC-NAV-007) deriva al usuario al módulo de
        // Vacantes en lugar de permitir el alta directa sin Vacante abierta.
        if (!await vacanteRepository.ExistsAbiertaByPuestoAsync(request.PuestoId, cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(
                    ErrorCategoria.Conflict,
                    OcupacionErrorCodigo.PuestoSinVacanteAbierta,
                    "El puesto no tiene una Vacante abierta; abra una Vacante antes de asignar una persona al puesto."));
        }

        // Issue 4: Check Persona+Puesto first (more specific), then Puesto alone.
        if (await ocupacionRepository.ExistsActiveByPersonaYPuestoAsync(request.PersonaId, request.PuestoId, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "PersonaYPuestoOcupados", "Ya existe una ocupación activa para la misma persona y puesto."));
        }

        if (await ocupacionRepository.ExistsActiveByPuestoAsync(request.PuestoId, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "PuestoOcupado", "Ya existe una ocupación activa para el puesto especificado."));
        }

        try
        {
            var ocupacion = new Ocupacion(
                request.PersonaId, request.PuestoId, request.FechaInicio,
                OcupacionTipoAsignacionMapper.ToDomain(request.TipoAsignacion), observaciones: request.Observaciones)
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
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", nameof(CrearAsync), ex.Message);
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "DatosInvalidos", ex.Message));
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
                new(ErrorCategoria.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var ocupacion = await ocupacionRepository.GetByIdIncludingHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        if (ocupacion is null)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.NotFound, "OcupacionNoEncontrada", "La ocupación no existe."));
        }

        if (!ocupacion.EsVigente)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "OcupacionNoEditable", "La ocupación no está activa y no se puede modificar."));
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
                new(ErrorCategoria.Conflict, "PersonaYPuestoOcupados", "Ya existe otra ocupación activa para la misma persona y puesto."));
        }

        if (await ocupacionRepository.ExistsActiveByPuestoAsync(request.PuestoId, id, cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "PuestoOcupado", "Ya existe otra ocupación activa para el puesto especificado."));
        }

        try
        {
            ocupacion.Actualizar(
                request.PersonaId,
                request.PuestoId,
                request.FechaInicio,
                OcupacionTipoAsignacionMapper.ToDomain(request.TipoAsignacion),
                request.Observaciones);

            await ocupacionRepository.UpdateAsync(ocupacion, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var personaNombre = $"{persona!.Nombres} {persona.Apellidos}";
            var puestoNombre = puesto!.Nombre;
            return OcupacionCommandResult.Success(MapToDto(ocupacion, personaNombre, puestoNombre));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", nameof(ActualizarAsync), ex.Message);
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "DatosInvalidos", ex.Message));
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
                new(ErrorCategoria.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var ocupacion = await ocupacionRepository.GetByIdIncludingHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        if (ocupacion is null)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.NotFound, "OcupacionNoEncontrada", "La ocupación no existe."));
        }

        if (!ocupacion.EsVigente)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "OcupacionNoEditable", "La ocupación no está activa y no se puede finalizar."));
        }

        try
        {
            ocupacion.Finalizar(request.FechaFin, request.Observaciones);

            await ocupacionRepository.UpdateAsync(ocupacion, cancellationToken).ConfigureAwait(false);

            // Fetch names BEFORE save — no post-commit reads.
            var persona = await personaRepository.GetByIdIncludingDeletedAsync(ocupacion.PersonaId, cancellationToken).ConfigureAwait(false);
            var puesto = await puestoRepository.GetByIdIncludingDeletedAsync(ocupacion.PuestoId, cancellationToken).ConfigureAwait(false);

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var personaNombre = persona is not null ? $"{persona.Nombres} {persona.Apellidos}" : "";
            var puestoNombre = puesto?.Nombre ?? "";

            return OcupacionCommandResult.Success(MapToDto(ocupacion, personaNombre, puestoNombre));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", nameof(FinalizarAsync), ex.Message);
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "FinalizacionInvalida", ex.Message));
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
                new(ErrorCategoria.NotFound, "OcupacionNoEncontrada", "La ocupación no existe."));
        }

        if (!ocupacion.EsVigente)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "OcupacionNoEditable", "La ocupación no está activa y no se puede eliminar."));
        }

        try
        {
            ocupacion.EliminarLogicamente();

            await ocupacionRepository.UpdateAsync(ocupacion, cancellationToken).ConfigureAwait(false);

            // Fetch names BEFORE save — no post-commit reads.
            var persona = await personaRepository.GetByIdIncludingDeletedAsync(ocupacion.PersonaId, cancellationToken).ConfigureAwait(false);
            var puesto = await puestoRepository.GetByIdIncludingDeletedAsync(ocupacion.PuestoId, cancellationToken).ConfigureAwait(false);

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var personaNombre = persona is not null ? $"{persona.Nombres} {persona.Apellidos}" : "";
            var puestoNombre = puesto?.Nombre ?? "";

            return OcupacionCommandResult.Success(MapToDto(ocupacion, personaNombre, puestoNombre));
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", nameof(EliminarAsync), ex.Message);
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "EliminacionInvalida", ex.Message));
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
                new(ErrorCategoria.NotFound, "OcupacionNoEncontrada", "La ocupación no existe."));
        }

        if (ocupacion.EsVigente)
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "OcupacionYaActiva", "La ocupación ya está activa."));
        }

        // Q2 (change vacante-ocupacion-flow-alignment): si la Ocupacion está
        // vinculada a una Vacante Cancelada, rechazar la reactivación.
        // Decisión pre-apply #1712: comparar por nombre literal
        // ("Cancelada") en vez de agregar columna/esCancelada — frágil ante
        // renombre del seed pero 0 migración adicional. Solo dispara en
        // ReactivarAsync; Finalizar y Eliminar no tocan este check
        // (preservación de Q1=NO reopen y Q3=NO reopen).
        if (ocupacion.VacanteId is { } vacanteVinculadaId)
        {
            var vacanteAsociada = await vacanteRepository
                .GetByIdForUpdateAsync(vacanteVinculadaId, cancellationToken)
                .ConfigureAwait(false);
            // FK rota histórica (vacante fue purgada) → permite reactivar.
            if (vacanteAsociada is not null
                && string.Equals(vacanteAsociada.EstadoVacante?.Nombre, "Cancelada", StringComparison.OrdinalIgnoreCase))
            {
                return OcupacionCommandResult.Failure(
                    new(
                        ErrorCategoria.Conflict,
                        OcupacionErrorCodigo.VacanteCanceladaParaReactivar,
                        "La Vacante asociada fue cancelada; no se puede reactivar la Ocupación."));
            }
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
                new(ErrorCategoria.Conflict, "PersonaYPuestoOcupados", "Ya existe una ocupación activa para la misma persona y puesto."));
        }

        if (await ocupacionRepository.ExistsActiveByPuestoAsync(ocupacion.PuestoId, id, cancellationToken).ConfigureAwait(false))
        {
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "PuestoOcupado", "Ya existe una ocupación activa para el puesto especificado."));
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
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", nameof(ReactivarAsync), ex.Message);
            return OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "ReactivacionInvalida", ex.Message));
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
                new(ErrorCategoria.Validation, "DatosInvalidos", "La persona no puede estar vacía."),
                fieldErrors), null);
        }

        var persona = await personaRepository.GetByIdIncludingDeletedAsync(personaId, cancellationToken).ConfigureAwait(false);
        if (persona is null)
        {
            return (OcupacionCommandResult.Failure(
                new(ErrorCategoria.NotFound, "PersonaNoEncontrada", "La persona referenciada no existe.")), null);
        }
        if (!persona.IsActive)
        {
            return (OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "PersonaInactiva", "La persona referenciada no está activa.")), null);
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
                new(ErrorCategoria.Validation, "DatosInvalidos", "El puesto no puede estar vacío."),
                fieldErrors), null);
        }

        var puesto = await puestoRepository.GetByIdIncludingDeletedAsync(puestoId, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return (OcupacionCommandResult.Failure(
                new(ErrorCategoria.NotFound, "PuestoNoEncontrado", "El puesto referenciado no existe.")), null);
        }
        if (!puesto.IsActive)
        {
            return (OcupacionCommandResult.Failure(
                new(ErrorCategoria.Conflict, "PuestoInactivo", "El puesto referenciado no está activo.")), null);
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
            OcupacionTipoAsignacionMapper.ToContract(ocupacion.TipoAsignacion),
            ocupacion.Observaciones,
            OcupacionEstadoHelper.CalcularEstado(ocupacion));
    }
}
