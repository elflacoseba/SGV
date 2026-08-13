namespace SGV.Contracts.Vacantes;

/// <summary>
/// Constantes de rutas HTTP consumidas por el cliente tipado
/// <c>VacanteApiClient</c> (capas <c>SGV.Web</c>).
/// <para>
/// <b>Legacy — rutas de Puestos dentro del namespace de Vacantes.</b>
/// Las constantes <see cref="PuestosBase"/> / <see cref="PuestosRoot"/> /
/// <see cref="PuestosDisponiblesBase"/> / <see cref="PuestosDisponiblesRoot"/>
/// viven aquí —no en <c>SGV.Contracts.Organizacion</c>— por el acoplamiento
/// histórico del cliente de Vacantes al dropdown de Puesto del formulario
/// Create (issue #235: la página no debe depender de
/// <c>IPuestosApiClient</c> cross-module). Centralizarlas en un futuro
/// <c>OrganizacionApiRoutes</c> requiere revisar el blast-radius de los
/// otros <c>*ApiClient</c> que hoy usan estas constantes indirectamente.
/// </para>
/// </summary>
public static class VacanteApiRoutes
{
    public const string Base = "api/v1/vacantes";
    public const string Root = "/" + Base;
    public const string ById = Root + "/{id:guid}";
    public const string CambiarEstado = ById + "/estado";

    public const string EstadosVacanteBase = "api/v1/estados-vacante";
    public const string EstadosVacanteRoot = "/" + EstadosVacanteBase;

    // El dropdown de Puesto en Create consume el endpoint existente de
    // Puestos (GET /api/v1/puestos). El cliente de Vacantes lo expone
    // como ListarPuestosAsync para que la página no dependa de
    // IPuestosApiClient cross-module (issue #235).
    public const string PuestosBase = "api/v1/puestos";
    public const string PuestosRoot = "/" + PuestosBase;

    // Endpoint dedicado para el dropdown de Puesto en Vacantes/Create
    // (REQ-PTO-DISP-001, defense-in-depth UX): devuelve únicamente puestos
    // sin Ocupación vigente ni Vacante abierta. La validación N1 y el
    // constraint ActivePuestoIdUnique siguen siendo la fuente de verdad
    // en el backend; este endpoint sólo evita fricción post-factum en el
    // formulario.
    public const string PuestosDisponiblesBase = PuestosBase + "/disponibles";
    public const string PuestosDisponiblesRoot = "/" + PuestosDisponiblesBase;

    public const string StatusQuery = "status";
    public const string PageQuery = "p";
    public const string PageSizeQuery = "pageSize";
    public const string SearchQuery = "search";
    public const string SortQuery = "sort";

    public const string StatusAbiertas = "abiertas";
    public const string StatusCerradas = "cerradas";
    public const string StatusTodas = "todas";

    // Sort whitelist values for VacanteRepository.ListarAsync.
    public const string SortFechaAperturaDesc = "fechaapertura_desc";
    public const string SortFechaAperturaAsc = "fechaapertura_asc";
    public const string SortPuestoAsc = "puesto_asc";
}
