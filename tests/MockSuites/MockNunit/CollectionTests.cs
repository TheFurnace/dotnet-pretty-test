namespace MockNunit;

[TestFixture]
public class CollectionTests
{
    [Test]
    public void List_AddItems_CountIsCorrect()
    {
        Console.WriteLine("Setting up collection test context...");

        var list = new List<string> { "alpha", "beta", "gamma" };

        Console.WriteLine($"List contents: {string.Join(", ", list)}");
        Assert.That(list, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task AsyncOperation_CompletesSuccessfully()
    {
        // simulates a slow I/O operation
        await Task.Delay(400);
        var result = await Task.FromResult(42);
        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void Dictionary_ContainsExpectedKey()
    {
        var dict = new Dictionary<string, int>
        {
            ["apples"] = 3,
            ["bananas"] = 5,
        };

        Assert.That(dict, Contains.Key("apples"));
        Assert.That(dict["apples"], Is.EqualTo(3));
    }

    [Test]
    public void Queue_DequeueOrder_IsFirstInFirstOut()
    {
        var queue = new Queue<int>(new[] { 10, 20, 30 });

        // intentionally wrong assertion — will fail
        Assert.That(queue.Dequeue(), Is.EqualTo(99));
    }

    [Test]
    public void Stack_PushPop_IsLastInFirstOut()
    {
        var stack = new Stack<string>();
        stack.Push("first");
        stack.Push("second");
        stack.Push("third");

        Assert.That(stack.Pop(), Is.EqualTo("third"));
        Assert.That(stack.Pop(), Is.EqualTo("second"));
    }

    [TestCase(new[] { 1, 2, 3, 4, 5 }, 5)]
    [TestCase(new[] { 10 },            1)]
    [TestCase(new int[] { },           0)]
    public void Array_LengthMatchesExpected(int[] input, int expectedLength)
    {
        Assert.That(input, Has.Length.EqualTo(expectedLength));
    }
}
