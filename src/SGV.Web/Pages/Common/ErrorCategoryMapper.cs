using SGV.Contracts.Comun;

namespace SGV.Web.Pages.Common;

/// <summary>
/// Centralized exhaustive switch over <see cref="ErrorCategoria"/> for all
/// 15 Razor PageModels that previously copy-pasted this pattern.
/// <para>
/// Customize entity-specific messages via optional parameters; the defaults
/// match the most common pattern (Cargos/Habilidades). Puestos and Unidades
/// Organizativas pass their own NotFound and Conflict messages.
/// </para>
/// </summary>
public static class ErrorCategoryMapper
{
    /// <summary>
    /// Maps an <see cref="ErrorCategoria"/> to a user-facing message.
    /// Throws <see cref="SwitchExpressionException"/> for unhandled values
    /// (no silent <c>default</c> — design §8.1, F3).
    /// </summary>
    public static string Map(
        ErrorCategoria categoria,
        string? notFoundMessage = null,
        string? conflictMessage = null,
        string? validationMessage = null)
    {
        return categoria switch
        {
            ErrorCategoria.NotFound => notFoundMessage ?? PageFeedback.NotFoundDeleteMessage,
            ErrorCategoria.Conflict => conflictMessage ?? "Conflicto al procesar la operación.",
            ErrorCategoria.Validation => validationMessage ?? "Revisá los datos ingresados.",
            ErrorCategoria.Unauthorized => PageFeedback.UnauthorizedMessage,
            ErrorCategoria.Forbidden => PageFeedback.ForbiddenMessage,
            ErrorCategoria.Transport => PageFeedback.TransportMessage,
            ErrorCategoria.Unexpected => PageFeedback.UnexpectedMessage,
            _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(
                $"Unhandled categoria: {categoria}"),
        };
    }
}
