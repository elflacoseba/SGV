using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// PageModel del detalle readonly de puestos. Carga un puesto por su
/// identificador vía <see cref="IPuestosApiClient.GetByIdAsync"/> y expone
/// la vista de solo lectura o un estado de no disponible cuando el puesto
/// no puede consultarse. Los parámetros <c>p</c>, <c>search</c>, <c>sort</c>
/// y <c>returnStatus</c> del query string se preservan en los enlaces de
/// retorno al listado y al Edit, manteniendo el contexto de navegación
/// cuando el usuario vuelve a <c>Index</c>.
/// </summary>
[Authorize]
public sealed class DetailsModel(IPuestosApiClient puestosApiClient, ILogger<DetailsModel> logger) : PageModel
{
    private const string DeletedView = "eliminadas";

    /// <summary>
    /// Datos del puesto obtenidos desde la API. <c>null</c> cuando el
    /// puesto no se encuentra o la consulta falla.
    /// </summary>
    public PuestoDto? Puesto { get; private set; }

    /// <summary>
    /// Indica si el puesto solicitado no pudo obtenerse (no encontrado
    /// o error de consulta). La vista debe mostrar un estado recuperable
    /// sin acciones de edición.
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Página del listado desde la que se navegó al detalle (se preserva
    /// en el enlace de retorno). Por defecto <c>1</c>.
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// Término de búsqueda activo al navegar al detalle (se preserva en
    /// los enlaces de retorno).
    /// </summary>
    public string? Search { get; private set; }

    /// <summary>
    /// Orden activo al navegar al detalle (se preserva en los enlaces de
    /// retorno).
    /// </summary>
    public string? Sort { get; private set; }

    /// <summary>
    /// Segmento del listado desde el que se navegó al detalle
    /// (<c>null</c> para activas, <c>"eliminadas"</c> para eliminadas).
    /// Se preserva como <c>status</c> en el enlace de retorno al Index
    /// y como <c>returnStatus</c> en los enlaces hacia otros detalles.
    /// </summary>
    public string? Segmento { get; private set; }

    /// <summary>
    /// Mensaje de feedback (success/warning/danger) entregado vía
    /// TempData tras una redirección PRG desde otra página (e.g. Edit).
    /// </summary>
    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    /// <summary>
    /// Tipo de feedback: <c>success</c>, <c>warning</c> o <c>danger</c>.
    /// Por defecto <c>success</c>.
    /// </summary>
    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    /// <summary>
    /// Handler GET del detalle readonly. Carga el puesto por id y, si no
    /// se encuentra o la consulta falla, marca <see cref="IsNotFound"/>.
    /// Los parámetros <c>p</c>, <c>search</c>, <c>sort</c> y
    /// <c>returnStatus</c> se preservan para los enlaces de retorno.
    /// </summary>
    public async Task OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);
        Segmento = NormalizeSegmento(returnStatus);

        try
        {
            Puesto = await puestosApiClient.GetByIdAsync(id, cancellationToken);

            if (Puesto is null)
            {
                IsNotFound = true;
                logger.LogWarning("Puesto with Id {PuestoId} was not found or is no longer available.", id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load puesto with Id {PuestoId}.", id);
            IsNotFound = true;
        }
    }

    /// <summary>
    /// Construye los route values para el enlace "Volver al listado".
    /// Preserva <c>p</c>, <c>search</c>, <c>sort</c> y el segmento
    /// (vía <c>status</c>) — espejo del patrón de retorno de
    /// <c>EditModel.ReturnToListUrl</c>.
    /// </summary>
    public object BuildIndexRouteValuesForReturn() => new
    {
        p = CurrentPage,
        search = Search,
        sort = Sort,
        status = Segmento
    };

    /// <summary>
    /// Construye los route values para el enlace "Editar" preservando
    /// el contexto de navegación (p, search, sort, returnStatus). El
    /// Edit del módulo Puestos acepta los mismos query params para
    /// preservar el back-link al Index.
    /// </summary>
    public object BuildEditRouteValuesForReturn(Guid id) => new
    {
        id,
        p = CurrentPage,
        search = Search,
        sort = Sort,
        returnStatus = Segmento
    };

    /// <summary>
    /// Construye la URL absoluta del detalle del puesto superior,
    /// preservando el contexto de navegación (p, search, sort,
    /// returnStatus). Espejo del helper
    /// <c>IndexModel.BuildDetailsUrl</c> pero del lado del PageModel
    /// de Details.
    /// </summary>
    public string BuildSuperiorUrl(Guid superiorId)
    {
        var parameters = new List<string> { $"id={superiorId:D}" };
        if (CurrentPage > 1)
        {
            parameters.Add($"p={CurrentPage}");
        }
        if (!string.IsNullOrEmpty(Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(Search!)}");
        }
        if (!string.IsNullOrEmpty(Sort))
        {
            parameters.Add($"sort={Uri.EscapeDataString(Sort!)}");
        }
        if (!string.IsNullOrEmpty(Segmento))
        {
            parameters.Add($"returnStatus={Uri.EscapeDataString(Segmento!)}");
        }

        return parameters.Count == 1
            ? $"/organizacion/puestos/detalles/{superiorId:D}"
            : $"/organizacion/puestos/detalles/{superiorId:D}?{string.Join("&", parameters)}";
    }

    /// <summary>URL absoluta al listado preservando el contexto.</summary>
    public string BuildIndexUrl()
    {
        var values = BuildIndexRouteValuesForReturn();
        return BuildQuery($"/organizacion/puestos", values);
    }

    /// <summary>URL absoluta al Edit preservando el contexto.</summary>
    public string BuildEditUrl(Guid id)
    {
        var values = BuildEditRouteValuesForReturn(id);
        return BuildQuery($"/organizacion/puestos/editar/{id:D}", values);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSegmento(string? returnStatus)
        => string.Equals(returnStatus, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null;

    /// <summary>
    /// Construye una URL absoluta con query string a partir de una base y
    /// un route-values anónimo. Sólo serializa las propiedades no-nulas
    /// para no contaminar el query string cuando el filtro no aplica.
    /// </summary>
    private static string BuildQuery(string basePath, object values)
    {
        var query = new List<string>();
        foreach (var prop in values.GetType().GetProperties())
        {
            var value = prop.GetValue(values);
            if (value is null)
            {
                continue;
            }

            var stringValue = value switch
            {
                string s when string.IsNullOrWhiteSpace(s) => null,
                int i when i == 1 => null, // Omitir p=1 (default)
                _ => value.ToString()
            };

            if (stringValue is null)
            {
                continue;
            }

            query.Add($"{Char.ToLowerInvariant(prop.Name[0])}{prop.Name[1..]}={Uri.EscapeDataString(stringValue!)}");
        }

        return query.Count == 0 ? basePath : $"{basePath}?{string.Join("&", query)}";
    }
}