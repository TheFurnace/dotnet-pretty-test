namespace TestLens.Trx;

public enum TestOutcome
{
    Passed,
    Failed,
    Skipped,
    NotExecuted,
}

public sealed record TestResult(
    string TestName,
    string FullyQualifiedName,
    TestOutcome Outcome,
    TimeSpan Duration,
    string? ErrorMessage,
    string? StackTrace,
    string? StdOut,
    string? StdErr
);

public sealed record ProjectRun(
    string ProjectName,
    string TrxPath,
    IReadOnlyList<TestResult> Results
)
{
    public int Total    => Results.Count;
    public int Passed   => Results.Count(r => r.Outcome == TestOutcome.Passed);
    public int Failed   => Results.Count(r => r.Outcome == TestOutcome.Failed);
    public int Skipped  => Results.Count(r => r.Outcome is TestOutcome.Skipped or TestOutcome.NotExecuted);

    public IEnumerable<TestResult> Failures => Results.Where(r => r.Outcome == TestOutcome.Failed);
}
