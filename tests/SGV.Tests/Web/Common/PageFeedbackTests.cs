using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SGV.Web.Integration.Common;
using Xunit;

namespace SGV.Tests.Web.Common;

public sealed class PageFeedbackTests
{
    [Fact]
    public void SetSuccess_StoresExistingTempDataKeysAndDefaultKindIsSuccess()
    {
        var tempData = new TempDataDictionary(new Microsoft.AspNetCore.Http.DefaultHttpContext(), new NullTempDataProvider());

        PageFeedback.SetSuccess(tempData, "Operación completada.");

        Assert.Equal("Operación completada.", PageFeedback.GetStatusMessage(tempData));
        Assert.Equal("success", PageFeedback.GetStatusKind(tempData));
        Assert.Equal("Operación completada.", tempData["StatusMessage"]);
        Assert.Equal("success", tempData["StatusKind"]);
    }

    [Fact]
    public void SetLastDeletedId_AndClearLastDeletedId_PreserveExistingKeyContract()
    {
        var deletedId = Guid.NewGuid();
        var tempData = new TempDataDictionary(new Microsoft.AspNetCore.Http.DefaultHttpContext(), new NullTempDataProvider());

        PageFeedback.SetLastDeletedId(tempData, deletedId);

        Assert.Equal(deletedId, PageFeedback.GetLastDeletedId(tempData));
        Assert.Equal(deletedId.ToString(), tempData["LastDeletedId"]);

        PageFeedback.ClearLastDeletedId(tempData);

        Assert.Null(PageFeedback.GetLastDeletedId(tempData));
        Assert.False(tempData.ContainsKey("LastDeletedId"));
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
