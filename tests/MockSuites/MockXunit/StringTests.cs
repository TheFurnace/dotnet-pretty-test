namespace MockXunit;

public class StringTests
{
    [Fact]
    public void Trim_RemovesLeadingAndTrailingWhitespace()
    {
        Console.WriteLine("Setting up string test context...");
        Console.WriteLine("Input: '  hello world  '");

        var result = "  hello world  ".Trim();

        Console.WriteLine($"Result: '{result}'");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ToUpper_ConvertsAllCharacters()
    {
        Assert.Equal("HELLO", "hello".ToUpper());
    }

    [Fact]
    public void Contains_IsCaseSensitive()
    {
        // intentionally wrong — Contains is case-sensitive by default,
        // so "World" is not found in "hello world"
        Assert.True("hello world".Contains("World"));
    }

    [Fact]
    public void Replace_SubstitutesAllOccurrences()
    {
        var result = "foo bar foo".Replace("foo", "baz");
        Assert.Equal("baz bar baz", result);
    }

    [Fact]
    public void Split_ReturnsCorrectPartCount()
    {
        var parts = "a,b,c,d".Split(',');
        Assert.Equal(4, parts.Length);
    }

    [Fact]
    public async Task SlowOperation_CompletesWithinTimeout()
    {
        // simulates a test that takes a noticeable amount of time
        await Task.Delay(600);
        Assert.True(true);
    }

    [Theory]
    [InlineData("hello", "HELLO")]
    [InlineData("world", "WORLD")]
    [InlineData("xUnit", "XUNIT")]
    [InlineData("", "")]
    public void ToUpper_Theory_VariousInputs(string input, string expected)
    {
        Assert.Equal(expected, input.ToUpper());
    }
}
