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
