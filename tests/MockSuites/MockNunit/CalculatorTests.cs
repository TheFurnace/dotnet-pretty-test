namespace MockNunit;

[TestFixture]
public class CalculatorTests
{
    [Test]
    public void Add_ReturnsCorrectSum()
    {
        Assert.That(2 + 3, Is.EqualTo(5));
    }

    [Test]
    public void Subtract_ReturnsCorrectDifference()
    {
        Assert.That(10 - 3, Is.EqualTo(7));
    }

    [TestCase(2,   2,   4)]
    [TestCase(0,   5,   5)]
    [TestCase(-3,  3,   0)]
    [TestCase(10, 10,  21)]  // wrong expected — will fail
    public void Add_TestCases(int a, int b, int expected)
    {
        Assert.That(a + b, Is.EqualTo(expected));
    }

    [Test]
    [Ignore("Pending refactor — divide-by-zero behaviour under review")]
    public void DivideByZero_ShouldThrow()
    {
        Assert.Throws<DivideByZeroException>(() => { int _ = 1 / 0; });
    }

    [Test]
    public void Multiply_NegativeNumbers_ReturnsPositive()
    {
        // intentionally wrong expected value to produce a failure
        Assert.That(-3 * -4, Is.EqualTo(99));
    }

    [TestCase(  2,   3,   6)]
    [TestCase(  0,   5,   0)]
    [TestCase( -2,   4,  -8)]
    public void Multiply_TestCases(int a, int b, int expected)
    {
        Assert.That(a * b, Is.EqualTo(expected));
    }
}
