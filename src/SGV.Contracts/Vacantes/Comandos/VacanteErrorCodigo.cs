namespace SGV.Contracts.Vacantes.Comandos;

public static class VacanteErrorCodigo
{
    public const string PuestoInexistente = nameof(PuestoInexistente);
    public const string EstadoVacanteInexistente = nameof(EstadoVacanteInexistente);
    public const string PuestoConVacanteAbierta = nameof(PuestoConVacanteAbierta);
    public const string VacanteInexistente = nameof(VacanteInexistente);
    public const string EstadoTerminalInmutable = nameof(EstadoTerminalInmutable);
    public const string MotivoObligatorio = nameof(MotivoObligatorio);
    public const string ObservacionesMuyLargas = nameof(ObservacionesMuyLargas);

    /// <summary>
    /// Generic validation error code used when FluentValidation fails
    /// without a per-field mapping (parity with Ocupaciones/Cargos).
    /// </summary>
    public const string DatosInvalidos = nameof(DatosInvalidos);
}
