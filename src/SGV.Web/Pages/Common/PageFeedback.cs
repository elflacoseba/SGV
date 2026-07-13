using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace SGV.Web.Pages.Common;

/// <summary>
/// Centralizes TempData keys used by Razor Pages feedback banners.
/// </summary>
public static class PageFeedback
{
    public const string StatusMessageKey = "StatusMessage";
    public const string StatusKindKey = "StatusKind";
    public const string LastDeletedIdKey = "LastDeletedId";

    // ─────────────────────────────────────────────────────────────────
    // Issue #125 / Slice 3: copy canónica para los mensajes inline de los
    // 14 PageModels que ahora ramifican por ErrorCategoria. Estas cadenas
    // son la fuente de verdad única; cualquier ajuste de wording debe
    // hacerse acá (no en cada switch individual). Verbatim del design §8.3.
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mensaje para <see cref="SGV.Contracts.Comun.ErrorCategoria.Transport"/>
    /// — fallo de red, timeout, body malformado.
    /// </summary>
    public const string TransportMessage = "No se pudo contactar al servicio. Intentá nuevamente.";

    /// <summary>
    /// Mensaje inline que antecede al redirect a login para
    /// <see cref="SGV.Contracts.Comun.ErrorCategoria.Unauthorized"/>.
    /// </summary>
    public const string UnauthorizedMessage = "Su sesión expiró. Vuelva a iniciar sesión.";

    /// <summary>
    /// Mensaje para <see cref="SGV.Contracts.Comun.ErrorCategoria.Forbidden"/>
    /// — usuario autenticado sin permisos suficientes.
    /// </summary>
    public const string ForbiddenMessage = "No tiene permisos para realizar esta operación.";

    /// <summary>
    /// Mensaje fallback para
    /// <see cref="SGV.Contracts.Comun.ErrorCategoria.Unexpected"/> — status
    /// code no anticipado por la matriz del mapper.
    /// </summary>
    public const string UnexpectedMessage = "Respuesta inesperada del servidor.";

    /// <summary>
    /// Mensaje para bajas donde el recurso ya no está disponible (404).
    /// </summary>
    public const string NotFoundDeleteMessage = "El recurso ya no está disponible.";

    public static string? GetStatusMessage(ITempDataDictionary tempData)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        return tempData[StatusMessageKey] as string;
    }

    public static string GetStatusKind(ITempDataDictionary tempData, string defaultKind = "success")
    {
        ArgumentNullException.ThrowIfNull(tempData);
        return tempData[StatusKindKey] as string ?? defaultKind;
    }

    public static void Set(ITempDataDictionary tempData, string message, string kind)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        tempData[StatusMessageKey] = message;
        tempData[StatusKindKey] = kind;
    }

    public static void SetSuccess(ITempDataDictionary tempData, string message) => Set(tempData, message, "success");

    public static void SetWarning(ITempDataDictionary tempData, string message) => Set(tempData, message, "warning");

    public static void SetDanger(ITempDataDictionary tempData, string message) => Set(tempData, message, "danger");

    public static void SetLastDeletedId(ITempDataDictionary tempData, Guid id)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        tempData[LastDeletedIdKey] = id.ToString();
    }

    public static Guid? GetLastDeletedId(ITempDataDictionary tempData)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        return Guid.TryParse(tempData[LastDeletedIdKey] as string, out var parsed) ? parsed : null;
    }

    public static void ClearLastDeletedId(ITempDataDictionary tempData)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        tempData.Remove(LastDeletedIdKey);
    }
}
