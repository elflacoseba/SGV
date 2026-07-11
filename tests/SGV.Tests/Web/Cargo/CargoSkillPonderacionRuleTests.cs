using CargoSkillPonderacionRule = SGV.Web.Integration.Organizacion.CargoSkillPonderacionRule;
using Xunit;

namespace SGV.Tests.Web.Cargo;

public sealed class CargoSkillPonderacionRuleTests
{
    [Fact]
    public void MinMaxAndErrorMessage_ExposeTheContractForPonderacionValidation()
    {
        Assert.Equal(0.01m, CargoSkillPonderacionRule.Min);
        Assert.Equal(100.00m, CargoSkillPonderacionRule.Max);
        Assert.Equal("La ponderación debe estar entre 0,01 y 100,00.", CargoSkillPonderacionRule.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    public void TryParse_BlankOrUnparseable_ReturnsInvalidAndNull(string? raw)
    {
        var (isValid, value) = CargoSkillPonderacionRule.TryParse(raw);

        Assert.False(isValid);
        Assert.Null(value);
    }

    [Theory]
    [InlineData("0.01", true, 0.01)]
    [InlineData("50.00", true, 50.00)]
    [InlineData("100.00", true, 100.00)]
    public void TryParse_WithinRange_ReturnsValidAndParsedValue(string raw, bool expectedValid, decimal expectedValue)
    {
        var (isValid, value) = CargoSkillPonderacionRule.TryParse(raw);

        Assert.Equal(expectedValid, isValid);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("0.00", 0.00)]
    [InlineData("100.01", 100.01)]
    [InlineData("-1.00", -1.00)]
    public void TryParse_OutOfRange_ReturnsInvalidButPreservesParsedValue(string raw, decimal expectedValue)
    {
        var (isValid, value) = CargoSkillPonderacionRule.TryParse(raw);

        Assert.False(isValid);
        Assert.Equal(expectedValue, value);
    }
}
