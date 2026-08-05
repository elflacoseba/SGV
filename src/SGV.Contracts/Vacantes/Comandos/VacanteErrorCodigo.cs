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
    public const string MotivoObligatorio = nameof(MotivoObligatorio);
    public const string ObservacionesMuyLargas = nameof(ObservacionesMuyLargas);

    /// <summary>
    /// 400 (N2 — al Cubrir una Vacante, <c>PersonaId</c> es obligatorio
    /// y debe venir provisto por la Postulación ganadora del módulo de
    /// Selección, fuera de scope de este change).
    /// </summary>
    public const string PersonaIdRequeridoParaCubrir = nameof(PersonaIdRequeridoParaCubrir);

    /// <summary>
    /// Generic validation error code used when FluentValidation fails
    /// without a per-field mapping (parity with Ocupaciones/Cargos).
    /// </summary>
    public const string DatosInvalidos = nameof(DatosInvalidos);
}
