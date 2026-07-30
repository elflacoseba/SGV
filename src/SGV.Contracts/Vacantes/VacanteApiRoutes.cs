namespace SGV.Contracts.Vacantes;

public static class VacanteApiRoutes
{
    public const string Base = "api/v1/vacantes";
    public const string Root = "/" + Base;
    public const string ById = Root + "/{id:guid}";
    public const string CambiarEstado = ById + "/estado";

    public const string EstadosVacanteBase = "api/v1/estados-vacante";
    public const string EstadosVacanteRoot = "/" + EstadosVacanteBase;

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
