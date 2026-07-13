using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace SGV.Web.Pages.Common;

/// <summary>
/// Encapsulates the list-return navigation context shared across Organizacion
/// PageModels (Puestos, UnidadesOrganizativas). Preserves pagination, search,
/// sort, view, and status filter state when navigating from a list to a
/// detail/create/edit page and back.
/// <para>
/// Each PageModel still keeps its own <c>[BindProperty]</c> fields for form
/// binding compatibility with existing <c>.cshtml</c> hidden inputs. This
/// helper standardizes the capture-from-query and URL-building patterns.
/// </para>
/// </summary>
public readonly record struct ReturnNavigationContext
{
    public string? Page { get; init; }
    public string? Search { get; init; }
    public string? Sort { get; init; }
    public string? View { get; init; }
    public string? Status { get; init; }

    /// <summary>
    /// Captures navigation context from query string parameters, resolving
    /// aliases (e.g. <c>p</c>/<c>page</c>/<c>returnPage</c> for Page).
    /// <c>null</c> and empty values are normalized to <c>null</c>.
    /// </summary>
    public static ReturnNavigationContext FromQuery(
        string? p = null,
        string? page = null,
        string? search = null,
        string? sort = null,
        string? view = null,
        string? returnPage = null,
        string? returnSearch = null,
        string? returnSort = null,
        string? returnView = null,
        string? returnStatus = null)
    {
        return new ReturnNavigationContext
        {
            Page = Normalize(returnPage ?? p ?? page),
            Search = Normalize(returnSearch ?? search),
            Sort = Normalize(returnSort ?? sort),
            View = Normalize(returnView ?? view),
            Status = NormalizeDeletedStatus(returnStatus),
        };
    }

    /// <summary>
    /// Captures navigation context from <c>[BindProperty]</c> + hidden-input
    /// values posted back in a form POST.
    /// </summary>
    public static ReturnNavigationContext FromForm(IFormCollection form)
    {
        return new ReturnNavigationContext
        {
            Page = NormalizePosted(form, "ReturnPage"),
            Search = NormalizePosted(form, "ReturnSearch"),
            Sort = NormalizePosted(form, "ReturnSort"),
            View = NormalizePosted(form, "ReturnView"),
            Status = NormalizeDeletedStatusPosted(form, "ReturnStatus"),
        };
    }

    /// <summary>
    /// Returns route values suitable for <c>RedirectToPage</c> or
    /// <c>Url.Page</c>, omitting <c>null</c> entries. Optionally includes
    /// an <c>id</c> parameter.
    /// </summary>
    public Dictionary<string, object?> ToRouteValues(Guid? id = null)
    {
        var values = new Dictionary<string, object?>();
        if (id.HasValue)
            values["id"] = id.Value;

        if (Page is not null)
            values["p"] = Page;
        if (Search is not null)
            values["search"] = Search;
        if (Sort is not null)
            values["sort"] = Sort;
        if (View is not null)
            values["returnView"] = View;
        if (Status is not null)
            values["returnStatus"] = Status;

        return values;
    }

    // ──────────────────────────────────────────────
    // Normalization helpers
    // ──────────────────────────────────────────────

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeDeletedStatus(string? value)
    {
        var normalized = Normalize(value);
        return string.Equals(normalized, "eliminadas", StringComparison.OrdinalIgnoreCase)
            ? "eliminadas"
            : null;
    }

    private static string? NormalizePosted(IFormCollection form, string key)
    {
        var val = form[key].FirstOrDefault();
        return Normalize(val);
    }

    private static string? NormalizeDeletedStatusPosted(IFormCollection form, string key)
    {
        var val = form[key].FirstOrDefault();
        return NormalizeDeletedStatus(val);
    }
}
