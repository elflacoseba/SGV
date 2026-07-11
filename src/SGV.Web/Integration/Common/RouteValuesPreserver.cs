using Microsoft.AspNetCore.Routing;

namespace SGV.Web.Integration.Common;

/// <summary>
/// Builds route values that preserve list context across Razor Page PRG flows.
/// </summary>
public static class RouteValuesPreserver
{
    public const string DeletedSegment = "eliminadas";

    public static RouteValueDictionary BuildListRouteValues(
        int page,
        string? search,
        string? sort,
        string? status = null,
        Guid? deletedId = null)
    {
        var values = new RouteValueDictionary();
        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        var normalizedStatus = NormalizeDeletedStatus(status);

        if (page > 1)
        {
            values["p"] = page;
        }

        if (normalizedSearch is not null)
        {
            values["search"] = normalizedSearch;
        }

        if (normalizedSort is not null)
        {
            values["sort"] = normalizedSort;
        }

        if (normalizedStatus is not null)
        {
            values["status"] = normalizedStatus;
        }

        if (deletedId.HasValue)
        {
            values["deletedId"] = deletedId.Value;
        }

        return values;
    }

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? NormalizeDeletedStatus(string? status) =>
        string.Equals(status, DeletedSegment, StringComparison.OrdinalIgnoreCase) ? DeletedSegment : null;
}
