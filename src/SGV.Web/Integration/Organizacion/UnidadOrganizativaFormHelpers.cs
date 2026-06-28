using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Helper methods for the create/edit form of organizational units.
/// </summary>
public static class UnidadOrganizativaFormHelpers
{
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
