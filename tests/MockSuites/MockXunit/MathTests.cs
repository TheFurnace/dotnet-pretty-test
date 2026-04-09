namespace MockXunit;

public class MathTests
{
    [Fact]
    public void Add_ReturnsCorrectSum()
    {
        Assert.Equal(5, 2 + 3);
    }

    [Fact]
    public void Subtract_ReturnsCorrectDifference()
    {
        Assert.Equal(7, 10 - 3);
    }

    [Fact]
    public void Divide_ByZero_ThrowsException()
    {
        Assert.Throws<DivideByZeroException>(() =>
        {
            int x = 1;
            int y = 0;
            _ = checked(x / y);
        });
    }

    [Fact]
    public void Multiply_LargeNumbers_ReturnsExpected()
    {
        // intentionally wrong expected value to produce a failure
        Assert.Equal(999, 123 * 456);
    }

    [Fact(Skip = "Not yet implemented — modulo edge cases pending spec")]
    public void Modulo_NegativeNumbers_BehavesCorrectly()
    {
        Assert.Equal(-1, -7 % 3);
    }

    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 1, 0)]
    [InlineData(100, 200, 301)]   // wrong expected — will fail
    public void Add_Theory_VariousInputs(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }
}
