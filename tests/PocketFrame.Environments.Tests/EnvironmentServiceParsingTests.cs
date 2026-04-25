using PocketFrame.Environments;
using Xunit;

namespace PocketFrame.Environments.Tests;

public sealed class EnvironmentServiceParsingTests
{
    [Theory]
    [InlineData("320x170", true)]
    [InlineData("1280X720", true)]
    [InlineData("0x170", false)]
    [InlineData("320", false)]
    public void ParsesVncGeometry(string value, bool expected)
    {
        Assert.Equal(expected, VncGeometry.TryParse(value, out _, out _));
    }

    [Fact]
    public void ShellQuoteEscapesSingleQuotes()
    {
        Assert.Equal("'it'\"'\"'s'", EnvironmentService.ShellQuote("it's"));
    }
}
