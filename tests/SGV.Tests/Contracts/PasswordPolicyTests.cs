using SGV.Contracts.Seguridad;
using Xunit;

namespace SGV.Tests.Contracts;

/// <summary>
/// Locks in the <see cref="PasswordPolicy"/> contract so any change to
/// <c>MinLength</c> or any of the require-flags fails CI before reaching
/// the wire. This guards against the I-1 release-readiness finding
/// (policy duplicated across 5 files) by giving the single source of
/// truth a direct test.
/// </summary>
public sealed class PasswordPolicyTests
{
    [Fact]
    public void Constants_ExposeExpectedValues()
    {
        Assert.Equal(6, PasswordPolicy.MinLength);
        Assert.True(PasswordPolicy.RequireLowercase);
        Assert.True(PasswordPolicy.RequireUppercase);
        Assert.True(PasswordPolicy.RequireDigit);
        Assert.True(PasswordPolicy.RequireNonAlphanumeric);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")] // whitespace-only is not enough — must have actual chars
    public void IsCompliant_NullOrEmpty_ReturnsFalse(string? password)
    {
        Assert.False(PasswordPolicy.IsCompliant(password));
    }

    [Theory]
    [InlineData("Aa1!aaa")]   // exactly MinLength, all classes
    [InlineData("Abc123!@#")]
    [InlineData("X9$zAAAA")]
    public void IsCompliant_AllRequiredClassesPresent_ReturnsTrue(string password)
    {
        Assert.True(PasswordPolicy.IsCompliant(password));
    }

    [Theory]
    [InlineData("Aa1!a")]     // too short (5 < MinLength)
    [InlineData("abcdef!")]   // no uppercase, no digit
    [InlineData("ABCDEF!")]   // no lowercase, no digit
    [InlineData("Aaabbbc")]   // no digit, no symbol
    [InlineData("Aa1bbbb")]   // no symbol
    public void IsCompliant_MissingRequiredClass_ReturnsFalse(string password)
    {
        Assert.False(PasswordPolicy.IsCompliant(password));
    }
}
