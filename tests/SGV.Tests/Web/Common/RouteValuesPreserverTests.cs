using Microsoft.AspNetCore.Routing;
using SGV.Web.Pages.Common;
using Xunit;

namespace SGV.Tests.Web.Common;

public sealed class RouteValuesPreserverTests
{
    [Fact]
    public void BuildListRouteValues_WithContext_ReturnsOnlyMeaningfulValues()
    {
        var deletedId = Guid.NewGuid();

        RouteValueDictionary values = RouteValuesPreserver.BuildListRouteValues(
            page: 3,
            search: " talento ",
            sort: "nombre_desc",
            status: "eliminadas",
            deletedId: deletedId);

        Assert.Equal(3, values["p"]);
        Assert.Equal("talento", values["search"]);
        Assert.Equal("nombre_desc", values["sort"]);
        Assert.Equal("eliminadas", values["status"]);
        Assert.Equal(deletedId, values["deletedId"]);
    }

    [Fact]
    public void BuildListRouteValues_ForDefaultActivasContext_OmitsNoise()
    {
        RouteValueDictionary values = RouteValuesPreserver.BuildListRouteValues(
            page: 1,
            search: " ",
            sort: null,
            status: "activas",
            deletedId: null);

        Assert.Empty(values);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  abc  ", "abc")]
    public void Normalize_ReturnsTrimmedValueOrNull(string? input, string? expected)
    {
        Assert.Equal(expected, RouteValuesPreserver.Normalize(input));
    }
}
