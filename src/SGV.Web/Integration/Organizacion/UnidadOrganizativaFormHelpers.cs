using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Helper methods for the create/edit form of organizational units.
/// </summary>
public static class UnidadOrganizativaFormHelpers
{
    public static string BuildReturnToListUrl(IUrlHelper url, string? page, string? search, string? sort, string? view = null)
    {
        var baseUrl = url.Page("/Organizacion/UnidadesOrganizativas/Index") ?? "/organizacion/unidades-organizativas";
        var query = new List<KeyValuePair<string, string?>>();

        if (!string.IsNullOrWhiteSpace(page))
        {
            query.Add(new KeyValuePair<string, string?>("p", page));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add(new KeyValuePair<string, string?>("search", search));
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            query.Add(new KeyValuePair<string, string?>("sort", sort));
        }

        if (string.Equals(view, "tree", StringComparison.OrdinalIgnoreCase))
        {
            query.Add(new KeyValuePair<string, string?>("view", "tree"));
        }

        return query.Count == 0
            ? baseUrl
            : $"{baseUrl}{QueryString.Create(query)}";
    }

    /// <summary>
    /// Flattens a hierarchical tree of organizational units into a list of parent-option view models,
    /// optionally excluding a specific unit and all its descendants (useful when editing a unit to
    /// prevent self- or descendant-parent assignment).
    /// </summary>
    public static IReadOnlyList<ParentOptionViewModel> FlattenTree(
        IReadOnlyList<UnidadOrganizativaTreeNodeDto> nodes,
        Guid? excludeSubtreeRootId = null,
        int depth = 0)
    {
        var result = new List<ParentOptionViewModel>();

        foreach (var node in nodes)
        {
            if (excludeSubtreeRootId.HasValue && node.Id == excludeSubtreeRootId.Value)
            {
                // Skip this node AND its descendants
                continue;
            }

            result.Add(new ParentOptionViewModel(node.Id.ToString(), $"{node.Codigo} — {node.Nombre}", depth));
            result.AddRange(FlattenTree(node.Hijas, excludeSubtreeRootId, depth + 1));
        }

        return result;
    }

    /// <summary>
    /// Maps a <see cref="ValidationProblemDetails"/> field-errors dictionary into ModelState entries.
    /// </summary>
    public static void ApplyFieldErrorsToModelState(
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState,
        IReadOnlyDictionary<string, string[]>? fieldErrors)
    {
        if (fieldErrors is null) return;

        foreach (var (key, messages) in fieldErrors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError($"Input.{key}", message);
            }
        }
    }
}
