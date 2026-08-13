namespace SGV.Contracts.Vacantes;

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

    // Cambio vacante-crear-puestos-libres (WU-4 / T-10): el dropdown de
    // Puesto en Vacantes/Create consume el sub-recurso dedicado
    // GET /api/v1/puestos/disponibles para mostrar únicamente puestos sin
    // Ocupación vigente ni Vacante Abierta (defense-in-depth UX; la
    // validación N1 + constraint ActivePuestoIdUnique siguen siendo la
    // fuente de verdad en el backend).
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
