using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Common;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Vacantes.Comandos.Validaciones;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Comun;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Vacantes;

namespace SGV.Aplicacion.Vacantes.Comandos;

/// <summary>
/// Default application service for Vacante write operations. Resolves
/// <c>R-WU3.1</c> (Crear) and <c>R-WU3.2</c> (CambiarEstado +
/// ActualizarObservaciones) per the orchestrator brief. Validators are
/// injected as <see cref="IValidator{T}"/> so the same DI composition
/// root can swap them out in tests (e.g. <c>VacanteServicioComandosTests</c>).
/// </summary>
public sealed class VacanteServicioComandos : IVacanteServicioComandos
{
    private readonly IVacanteRepository vacanteRepository;
    private readonly IEstadoVacanteRepository estadoVacanteRepository;
    private readonly IOcupacionRepository ocupacionRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IConstraintViolationDetector constraintDetector;
    private readonly ILogger<VacanteServicioComandos> logger;
    private readonly IValidator<CrearVacanteRequest> crearValidator;
    private readonly IValidator<CambiarEstadoVacanteRequest> cambiarEstadoValidator;

    /// <summary>
    /// Primary constructor with the full DI dependency set.
    /// </summary>
    public VacanteServicioComandos(
        IVacanteRepository vacanteRepository,
        IEstadoVacanteRepository estadoVacanteRepository,
        IUnitOfWork unitOfWork,
        IConstraintViolationDetector constraintDetector,
        ILogger<VacanteServicioComandos> logger,
        IValidator<CrearVacanteRequest> crearValidator,
        IValidator<CambiarEstadoVacanteRequest> cambiarEstadoValidator,
        IOcupacionRepository ocupacionRepository)
    {
        ArgumentNullException.ThrowIfNull(vacanteRepository);
        ArgumentNullException.ThrowIfNull(estadoVacanteRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(constraintDetector);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(crearValidator);
        ArgumentNullException.ThrowIfNull(cambiarEstadoValidator);
        ArgumentNullException.ThrowIfNull(ocupacionRepository);

        this.vacanteRepository = vacanteRepository;
        this.estadoVacanteRepository = estadoVacanteRepository;
        this.ocupacionRepository = ocupacionRepository;
        this.unitOfWork = unitOfWork;
        this.constraintDetector = constraintDetector;
        this.logger = logger;
        this.crearValidator = crearValidator;
        this.cambiarEstadoValidator = cambiarEstadoValidator;
    }

    /// <summary>
    /// Convenience constructor for tests and simple registration.
    /// Uses the real validators directly.
    /// </summary>
    public VacanteServicioComandos(
        IVacanteRepository vacanteRepository,
        IEstadoVacanteRepository estadoVacanteRepository,
        IUnitOfWork unitOfWork,
        IConstraintViolationDetector constraintDetector,
        ILogger<VacanteServicioComandos> logger,
        IOcupacionRepository ocupacionRepository)
        : this(
            vacanteRepository, estadoVacanteRepository, unitOfWork, constraintDetector, logger,
            new CrearVacanteRequestValidator(),
            new CambiarEstadoVacanteRequestValidator(),
            ocupacionRepository)
    {
    }

    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
        => ValidationHelper.BuildFieldErrors(failures);

    private static string ToCamelCase(string propertyName)
        => ValidationHelper.ToCamelCase(propertyName);

    // ── CrearAsync ─────────────────────────────────────────────

    /// <summary>
    /// R-WU3.1 — Opens a new vacante. Validates request shape, enforces
    /// the "one open vacante per puesto" rule (closes S-1: vacantes cannot
    /// coexist on the same puesto while at least one is in a non-terminal
    /// estado). Persists the new aggregate and returns its detail DTO.
    /// </summary>
    /// <remarks>
    /// Anti-race strategy: el contrato <c>IUnitOfWork</c> del repo no
    /// expone <c>BeginTransaction</c> y la BD no impone un índice unique
    /// activo por <c>PuestoId</c> (R-5 del proposal). La verificación a
    /// nivel servicio (<see cref="IVacanteRepository.ExistsAbiertaByPuestoAsync"/>)
    /// es la defensa principal; aceptamos el riesgo TOCTOU entre la
    /// verificación y el <c>SaveChangesAsync</c> porque la operación es
    /// de baja frecuencia (apertura manual por GestorVacantes) y porque
    /// la consistencia fuerte requiere un cambio de esquema (índices
    /// parciales sobre columnas generadas). Documentado en
    /// <c>apply-progress.md §Deviations</c>.
    /// </remarks>
    public async Task<VacanteCommandResult> CrearAsync(
        CrearVacanteRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Validation,
                    VacanteErrorCodigo.DatosInvalidos,
                    "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var estadoVacante = await estadoVacanteRepository
            .GetByIdAsync(request.EstadoVacanteId, cancellationToken)
            .ConfigureAwait(false);
        if (estadoVacante is null)
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.NotFound,
                    VacanteErrorCodigo.EstadoVacanteInexistente,
                    "El estado de vacante referenciado no existe."));
        }

        // El estado inicial no puede ser terminal: "abrir vacante" requiere estado no terminal.
        // Nota: el código es el mismo que CambiarEstadoAsync (409 Conflict); aquí es 400
        // porque la solicitud es inválida antes de persistir. Ver design §Decisiones.
        if (estadoVacante.EsTerminal)
        {
            const string mensaje = "El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada).";
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Validation,
                    VacanteErrorCodigo.EstadoTerminalInmutable,
                    mensaje),
                new Dictionary<string, string[]> { ["estadoVacanteId"] = [mensaje] });
        }

        // N1 (change vacante-ocupacion-flow-alignment): una Ocupación
        // activa implica que la posición del Puesto está ocupada. Abrir
        // una Vacante para el mismo Puesto es incoherente; rechazar antes
        // de la constraint BD para dar un error semánticamente correcto
        // desde el dominio. Distinto de PuestoConVacanteAbierta (que
        // rechaza por otra Vacante abierta) y del PuestoOcupado del lado
        // Ocupación (que es la unicidad por Puesto desde ese módulo).
        if (await ocupacionRepository.ExistsActiveByPuestoAsync(request.PuestoId, excludingId: null, cancellationToken).ConfigureAwait(false))
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Conflict,
                    VacanteErrorCodigo.PuestoOcupado,
                    "El puesto tiene una Ocupación activa; no se puede abrir una vacante mientras la posición esté ocupada."));
        }

        if (await vacanteRepository.ExistsAbiertaByPuestoAsync(request.PuestoId, cancellationToken).ConfigureAwait(false))
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Conflict,
                    VacanteErrorCodigo.PuestoConVacanteAbierta,
                    "Ya existe una vacante abierta para el puesto especificado."));
        }

        try
        {
            var vacante = new Vacante(
                request.PuestoId,
                request.EstadoVacanteId,
                request.FechaApertura,
                request.Motivo)
            {
                Id = Guid.NewGuid()
            };

            if (!string.IsNullOrWhiteSpace(request.Observaciones))
            {
                vacante.ActualizarObservaciones(request.Observaciones);
            }

            await vacanteRepository.AddAsync(vacante, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var detailDto = MapToDetailDto(vacante, estadoNombre: estadoVacante.Nombre, historial: []);
            return VacanteCommandResult.Success(detailDto);
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", nameof(CrearAsync), ex.Message);
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Conflict,
                    VacanteErrorCodigo.PuestoConVacanteAbierta,
                    "Ya existe una vacante abierta para el puesto especificado."));
        }
    }

    // ── CambiarEstadoAsync ────────────────────────────────────

    /// <summary>
    /// R-WU3.2 (CambiarEstado) — Transitions a vacante. Loads the tracked
    /// aggregate via <see cref="IVacanteRepository.GetByIdForUpdateAsync"/>,
    /// rejects terminal→non-terminal transitions
    /// (<see cref="VacanteErrorCodigo.EstadoTerminalInmutable"/>), invokes
    /// <c>Vacante.CambiarEstado</c> on the domain, and forwards the
    /// resulting <see cref="HistorialEstadoVacante"/> to
    /// <see cref="IVacanteRepository.RegistrarCambioEstadoAsync"/> which
    /// re-fetches the tracked entity, applies <c>UpdateEntity</c>, and
    /// adds the new history row to <c>entity.HistorialEstados</c> so EF
    /// wraps both writes in a single transaction at
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> time (atomicidad,
    /// <c>design.md</c> §D-5).
    /// </summary>
    public async Task<VacanteCommandResult> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoVacanteRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await cambiarEstadoValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Validation,
                    VacanteErrorCodigo.DatosInvalidos,
                    "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var vacante = await vacanteRepository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (vacante is null)
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.NotFound,
                    VacanteErrorCodigo.VacanteInexistente,
                    "La vacante no existe."));
        }

        var estadoActual = await estadoVacanteRepository
            .GetByIdAsync(vacante.EstadoVacanteId, cancellationToken)
            .ConfigureAwait(false);
        if (estadoActual is null)
        {
            // FK rota hacia EstadoVacante: tratamos como vacante corrupta.
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Unexpected,
                    VacanteErrorCodigo.EstadoVacanteInexistente,
                    "El estado actual de la vacante no existe en el catálogo."));
        }

        if (estadoActual.EsTerminal)
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Conflict,
                    VacanteErrorCodigo.EstadoTerminalInmutable,
                    "La vacante está en un estado terminal y no admite más cambios."));
        }

        var estadoNuevo = await estadoVacanteRepository
            .GetByIdAsync(request.EstadoVacanteId, cancellationToken)
            .ConfigureAwait(false);
        if (estadoNuevo is null)
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.NotFound,
                    VacanteErrorCodigo.EstadoVacanteInexistente,
                    "El estado de vacante destino no existe."));
        }

        // N2 (change vacante-ocupacion-flow-alignment): Cubrir una Vacante
        // requiere PersonaId (provisto por la Postulación ganadora, fuera
        // de scope). Decisión pre-apply: comparar destinoCubierta por
        // nombre literal ("Cubierta") en vez de agregar una columna —
        // mismo trade-off que T-5.0 vs Cancelada, frágil ante renombre
        // del seed pero 0 migración. Cuando el destino no es Cubierta
        // (e.g. Cancelada o no terminal), el campo PersonaId se ignora.
        var destinoEsCubierta = estadoNuevo.EsTerminal
            && string.Equals(estadoNuevo.Nombre, "Cubierta", StringComparison.OrdinalIgnoreCase);
        if (destinoEsCubierta && request.PersonaId is null)
        {
            var fieldErrors = new Dictionary<string, string[]>
            {
                ["personaId"] = ["PersonaId es obligatorio al cubrir una Vacante."]
            };
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Validation,
                    VacanteErrorCodigo.PersonaIdRequeridoParaCubrir,
                    "PersonaId es obligatorio al cubrir una Vacante."),
                fieldErrors);
        }

        try
        {
            var historial = vacante.CambiarEstado(
                estadoNuevoId: request.EstadoVacanteId,
                usuarioId: null,
                motivo: request.Motivo,
                cerrar: estadoNuevo.EsTerminal);

            if (!string.IsNullOrWhiteSpace(request.Observaciones))
            {
                vacante.ActualizarObservaciones(request.Observaciones);
            }

            await vacanteRepository.RegistrarCambioEstadoAsync(vacante, historial, cancellationToken).ConfigureAwait(false);

            // N2: al Cubrir, crear la Ocupacion derivada en la MISMA
            // transacción EF (una sola SaveChanges más abajo). EF agrupa
            // ambas inserciones (vacante + historial + ocupacion) en un
            // solo commit; si falla la Ocupacion, el cambio de estado de
            // la Vacante también se revierte. El check de PersonaId arriba
            // garantiza null-safety aquí.
            if (destinoEsCubierta)
            {
                var ocupacionDerivada = new Ocupacion(
                    personaId: request.PersonaId!.Value,
                    puestoId: vacante.PuestoId,
                    fechaInicio: DateOnly.FromDateTime(DateTime.UtcNow),
                    tipoAsignacion: TipoAsignacion.Permanente,
                    fechaFin: null,
                    observaciones: null,
                    vacanteId: vacante.Id);

                await ocupacionRepository
                    .AddAsync(ocupacionDerivada, cancellationToken)
                    .ConfigureAwait(false);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var detailDto = MapToDetailDto(
                vacante,
                estadoNombre: estadoNuevo.Nombre,
                historial: [historial]);
            return VacanteCommandResult.Success(detailDto);
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", nameof(CambiarEstadoAsync), ex.Message);
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Conflict,
                    VacanteErrorCodigo.DatosInvalidos,
                    ex.Message));
        }
    }

    // ── ActualizarObservacionesAsync ───────────────────────────

    /// <summary>
    /// R-WU3.2 (ActualizarObservaciones) — Updates the free-form
    /// <c>Observaciones</c> field of a vacante. Delegates the validation
    /// to the domain (<c>≤500</c> chars, null/empty/whitespace cleared)
    /// and the persistence to
    /// <see cref="IVacanteRepository.UpdateAsync"/>. PB-3 nota:
    /// <paramref name="observaciones"/> es opcional; el método acepta
    /// null y el dominio lo normaliza.
    /// </summary>
    public async Task<VacanteCommandResult> ActualizarObservacionesAsync(
        Guid id,
        string? observaciones,
        CancellationToken cancellationToken = default)
    {
        // Validación previa opcional para evitar round-trip al repo cuando
        // el input supera el límite (≤500). El dominio también valida pero
        // lanza ArgumentException; preferimos Failure con ErrorCategoria.
        if (observaciones is not null && observaciones.Length > 500)
        {
            var fieldErrors = new Dictionary<string, string[]>
            {
                [ToCamelCase(nameof(observaciones))] = ["Las observaciones no pueden superar 500 caracteres."]
            };
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Validation,
                    VacanteErrorCodigo.ObservacionesMuyLargas,
                    "Las observaciones exceden la longitud máxima permitida."),
                fieldErrors);
        }

        var vacante = await vacanteRepository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (vacante is null)
        {
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.NotFound,
                    VacanteErrorCodigo.VacanteInexistente,
                    "La vacante no existe."));
        }

        try
        {
            vacante.ActualizarObservaciones(observaciones);
            await vacanteRepository.UpdateAsync(vacante, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // `GetByIdForUpdateAsync` carga `EstadoVacante` eager-loaded,
            // por lo que el nombre ya está disponible en la nav property.
            // Esto evita un round-trip extra a BD; si la navegación viniera
            // null (caso anómalo de FK rota), caemos a string.Empty como
            // hace el resto del módulo.
            var estadoNombre = vacante.EstadoVacante?.Nombre ?? string.Empty;
            var detailDto = MapToDetailDto(
                vacante,
                estadoNombre: estadoNombre,
                historial: []);
            return VacanteCommandResult.Success(detailDto);
        }
        catch (DbUpdateException ex) when (constraintDetector.IsConstraintViolation(ex))
        {
            logger.LogWarning(ex, "Constraint violation in {Method}: {Message}", nameof(ActualizarObservacionesAsync), ex.Message);
            return VacanteCommandResult.Failure(
                new VacanteError(
                    ErrorCategoria.Conflict,
                    VacanteErrorCodigo.DatosInvalidos,
                    ex.Message));
        }
    }

    // ── Mappers ────────────────────────────────────────────────

    private static VacanteDetailDto MapToDetailDto(
        Vacante vacante,
        string estadoNombre,
        IReadOnlyList<HistorialEstadoVacante> historial)
    {
        var puestoNombre = vacante.Puesto?.Nombre ?? string.Empty;
        var historialDtos = historial
            .Select(h => new HistorialEstadoVacanteDto(
                EstadoAnteriorNombre: h.EstadoAnterior?.Nombre,
                EstadoNuevoNombre: ResolveNombreEstado(h.EstadoNuevo, estadoNombre),
                ChangedAt: h.ChangedAt,
                ChangedByUserId: h.ChangedByUserId,
                Motivo: h.Motivo))
            .ToArray();

        return new VacanteDetailDto(
            vacante.Id,
            vacante.PuestoId,
            puestoNombre,
            vacante.EstadoVacanteId,
            estadoNombre,
            vacante.FechaApertura,
            vacante.FechaCierre,
            vacante.Motivo,
            vacante.Observaciones,
            historialDtos);
    }

    private static string ResolveNombreEstado(EstadoVacante? estadoNuevo, string fallback)
        => estadoNuevo?.Nombre ?? fallback;
}