namespace SGV.Contracts.Vacantes.Comandos;

public static class VacanteErrorCodigo
{
    public const string PuestoInexistente = nameof(PuestoInexistente);
    public const string EstadoVacanteInexistente = nameof(EstadoVacanteInexistente);

    /// <summary>
    /// 409: la constraint BD <c>ActivePuestoIdUnique</c> rechaza la
    /// creación de una Vacante abierta cuando ya existe otra Vacante
    /// abierta para el mismo Puesto. Origen: índice único generado en
    /// <c>Vacantes</c>.
    /// </summary>
    public const string PuestoConVacanteAbierta = nameof(PuestoConVacanteAbierta);

    /// <summary>
    /// 409 (N1 del change <c>vacante-ocupacion-flow-alignment</c>):
    /// el Puesto tiene una <c>Ocupacion</c> activa para el mismo
    /// PuestoId. Distinto de <see cref="PuestoConVacanteAbierta"/>:
    /// ese rechaza por Vacante abierta (otra Vacante), este rechaza
    /// por Ocupacion activa (paridad con
    /// <see cref="Ocupaciones.Comandos.OcupacionErrorCodigo.PuestoOcupado"/>
    /// que ya existe para la unicidad por Puesto desde el lado Ocupación).
    /// </summary>
    public const string PuestoOcupado = nameof(PuestoOcupado);

    public const string VacanteInexistente = nameof(VacanteInexistente);
    public const string EstadoTerminalInmutable = nameof(EstadoTerminalInmutable);
    public const string ObservacionesMuyLargas = nameof(ObservacionesMuyLargas);

    /// <summary>
    /// 400 (N2 — al Cubrir una Vacante vía <c>PATCH /estado</c>, el
    /// flujo correcto es crear una Ocupación vía
    /// <c>POST /api/v1/ocupaciones</c> con <c>VacanteId</c>. La transición
    /// directa a <c>Cubierta</c> está deprecada; este código rechaza el
    /// path legacy y deriva al botón "Cubrir Vacante" del Details.
    /// Change <c>invertir-flujo-cubrir</c>.
    /// </summary>
    public const string CubrirVacanteRequiereCrearOcupacion = nameof(CubrirVacanteRequiereCrearOcupacion);

    /// <summary>
    /// 400 (N2 — nombre legacy conservado para compatibilidad de clientes
    /// cacheados). Refleja el flujo anterior: al Cubrir una Vacante,
    /// <c>PersonaId</c> era obligatorio. Reemplazado por
    /// <see cref="CubrirVacanteRequiereCrearOcupacion"/> en el change
    /// <c>invertir-flujo-cubrir</c>: el servicio NUNCA devuelve este código
    /// en runtime post-change; los tests nuevos referencian exclusivamente
    /// el nuevo nombre.
    /// </summary>
    [Obsolete("Use CubrirVacanteRequiereCrearOcupacion. El flujo Cubrir vive en OcupacionServicioComandos.CrearAsync con VacanteId; este código ya no se devuelve en runtime.")]
    public const string PersonaIdRequeridoParaCubrir = nameof(PersonaIdRequeridoParaCubrir);

    /// <summary>
    /// Generic validation error code used when FluentValidation fails
    /// without a per-field mapping (parity with Ocupaciones/Cargos).
    /// </summary>
    public const string DatosInvalidos = nameof(DatosInvalidos);
}
