namespace SGV.Contracts.Ocupaciones;

public static class OcupacionApiRoutes
{
    public const string Base = "api/v1/ocupaciones";
    public const string Root = "/" + Base;
    public const string ById = Root + "/{id:guid}";
    public const string Finalize = ById + "/finalizar";
    public const string Reactivate = ById + "/reactivar";

    public const string StatusQuery = "status";
    public const string PersonaIdQuery = "personaId";
    public const string PuestoIdQuery = "puestoId";
    public const string PageQuery = "page";
    public const string PageSizeQuery = "pageSize";
    public const string SearchQuery = "search";
    public const string SortQuery = "sort";

    // Sort whitelist values for OcupacionRepository.QueryAsync.
    public const string SortFechaInicioAsc = "fechainicio_asc";
    public const string SortPersonaAsc = "persona_asc";
    public const string SortPersonaDesc = "persona_desc";
    public const string SortPuestoAsc = "puesto_asc";
    public const string SortPuestoDesc = "puesto_desc";
}
