using TestLens.Trx;

namespace TestLens.Tests.Trx;

public class TrxParserTests
{
    private static readonly string SampleTrx =
        Path.Combine(AppContext.BaseDirectory, "sample.trx");

    [Fact]
    public void Parse_ReturnsCorrectTotalCount()
    {
        var run = TrxParser.Parse(SampleTrx);
        Assert.Equal(5, run.Total);
    }

    [Fact]
    public void Parse_ReturnsCorrectPassedCount()
    {
        var run = TrxParser.Parse(SampleTrx);
        Assert.Equal(1, run.Passed);
    }

    [Fact]
    public void Parse_ReturnsCorrectFailedCount()
    {
        var run = TrxParser.Parse(SampleTrx);
        Assert.Equal(3, run.Failed);
    }

    [Fact]
    public void Parse_ReturnsCorrectSkippedCount()
    {
        var run = TrxParser.Parse(SampleTrx);
        Assert.Equal(1, run.Skipped);
    }

    [Fact]
    public void Parse_FailureHasErrorMessage()
    {
        var run = TrxParser.Parse(SampleTrx);
        var failure = run.Failures.First(f => f.TestName == "ShouldAddNumbers");
        Assert.Contains("Expected: 42", failure.ErrorMessage);
    }

    [Fact]
    public void Parse_FailureHasStackTrace()
    {
        var run = TrxParser.Parse(SampleTrx);
        var failure = run.Failures.First(f => f.TestName == "ShouldAddNumbers");
        Assert.Contains("MathTests.cs", failure.StackTrace);
    }

    [Fact]
    public void Parse_FailureHasStdOut()
    {
        var run = TrxParser.Parse(SampleTrx);
        var failure = run.Failures.First(f => f.TestName == "ShouldAddNumbers");
        Assert.Contains("Setting up test context", failure.StdOut);
    }

    [Fact]
    public void Parse_FailureHasStdErr()
    {
        var run = TrxParser.Parse(SampleTrx);
        var failure = run.Failures.First(f => f.TestName == "ShouldTimeout");
        Assert.Contains("connection pool exhausted", failure.StdErr);
    }

    [Fact]
    public void Parse_PassedTestHasNullErrorInfo()
    {
        var run = TrxParser.Parse(SampleTrx);
        var passed = run.Results.First(r => r.TestName == "ShouldPassCleanly");
        Assert.Null(passed.ErrorMessage);
        Assert.Null(passed.StackTrace);
    }

    [Fact]
    public void Parse_InferredProjectNameFromTrxPath()
    {
        var run = TrxParser.Parse(SampleTrx);
        // sample.trx has no storage attribute, so falls back to filename
        Assert.Equal("sample", run.ProjectName);
    }

    [Fact]
    public void ParsePartial_DoesNotThrowOnMissingFile()
    {
        var run = TrxParser.ParsePartial("/nonexistent/path/results.trx");
        Assert.Equal(0, run.Total);
    }
}
