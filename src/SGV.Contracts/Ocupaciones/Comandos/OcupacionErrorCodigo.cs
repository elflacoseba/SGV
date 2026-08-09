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

    /// <summary>
    /// 404 (REQ-OCC-FORM-010, change <c>invertir-flujo-cubrir</c>):
    /// <c>VacanteId</c> provisto en <c>CrearOcupacionRequest</c> no
    /// resuelve ninguna Vacante persistida.
    /// </summary>
    public const string VacanteNoEncontrada = nameof(VacanteNoEncontrada);

    /// <summary>
    /// 400 (REQ-OCC-FORM-010, change <c>invertir-flujo-cubrir</c>):
    /// la Vacante referenciada está en un estado terminal (<c>Cubierta</c>
    /// o <c>Cancelada</c>); no admite cobertura. La operación correcta es
    /// abrir una nueva Vacante.
    /// </summary>
    public const string VacanteNoAbierta = nameof(VacanteNoAbierta);

    /// <summary>
    /// 409 (REQ-OCC-FORM-010, change <c>invertir-flujo-cubrir</c>):
    /// la Vacante referenciada ya tiene una Ocupación vigente vinculada
    /// (<c>EsVigente=true</c> y <c>VacanteId</c> coincidente); no se crea
    /// una segunda Ocupación derivada.
    /// </summary>
    public const string VacanteYaCubierta = nameof(VacanteYaCubierta);

    /// <summary>
    /// 400 (REQ-OCC-FORM-010, change <c>invertir-flujo-cubrir</c>):
    /// el <c>PuestoId</c> del request no coincide con el <c>PuestoId</c>
    /// de la Vacante referenciada. La API devuelve este código en lugar
    /// de inferir el Puesto desde la Vacante cuando el cliente lo envía
    /// explícitamente.
    /// </summary>
    public const string PuestoIdNoCoincideConVacante = nameof(PuestoIdNoCoincideConVacante);
}
