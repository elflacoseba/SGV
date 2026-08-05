namespace SGV.Contracts.Ocupaciones.Comandos;

public static class OcupacionErrorCodigo
{
    public const string PersonaYPuestoOcupados = nameof(PersonaYPuestoOcupados);
    public const string PuestoOcupado = nameof(PuestoOcupado);
    public const string PersonaInactiva = nameof(PersonaInactiva);
    public const string PuestoInactivo = nameof(PuestoInactivo);
    public const string FechaFinInvalida = nameof(FechaFinInvalida);
    public const string OcupacionYaActiva = nameof(OcupacionYaActiva);

    /// <summary>
    /// 409 (N3 del change <c>vacante-ocupacion-flow-alignment</c>):
    /// la Ocupacion directa sobre un Puesto requiere que exista una
    /// Vacante abierta para ese Puesto. Si no existe, el alta se
    /// rechaza y la UI debe derivar al flujo de Vacantes (REQ-OCC-FORM-009
    /// / REQ-OCC-NAV-007). Sin excepciones por rol: Q5=N3 absoluto.
    /// </summary>
    public const string PuestoSinVacanteAbierta = nameof(PuestoSinVacanteAbierta);

    /// <summary>
    /// 409 (Q2 del change <c>vacante-ocupacion-flow-alignment</c>):
    /// la Ocupacion está vinculada a una Vacante que fue Cancelada.
    /// Reactivarla reabriría la posición ocupada por la Cancelación
    /// administrativa; se rechaza para preservar la decisión de negocio.
    /// Solo dispara en <c>ReactivarAsync</c>, no en Finalizar ni Eliminar
    /// (preservación de Q1=NO reopen y Q3=NO reopen).
    /// </summary>
    public const string VacanteCanceladaParaReactivar = nameof(VacanteCanceladaParaReactivar);
}
