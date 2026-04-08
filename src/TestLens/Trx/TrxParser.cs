using System.Xml.Linq;

namespace TestLens.Trx;

public static class TrxParser
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static ProjectRun Parse(string trxPath)
    {
        var projectName = InferProjectName(trxPath);
        var results = ParseResults(trxPath);
        return new ProjectRun(projectName, trxPath, results);
    }

    public static ProjectRun ParsePartial(string trxPath)
    {
        // Same as Parse but tolerates an incomplete/still-being-written file
        try
        {
            return Parse(trxPath);
        }
        catch
        {
            // File mid-write — return empty shell so progress display doesn't crash
            return new ProjectRun(InferProjectName(trxPath), trxPath, []);
        }
    }

    private static List<TestResult> ParseResults(string trxPath)
    {
        XDocument doc;
        using (var stream = new FileStream(trxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            doc = XDocument.Load(stream);

        var root = doc.Root ?? throw new InvalidDataException("TRX file has no root element.");

        // Build a map of testId → definition info (FQN)
        var definitions = root
            .Descendants(Ns + "UnitTest")
            .Select(u => new
            {
                Id  = (string?)u.Element(Ns + "Execution")?.Attribute("id") ?? "",
                Fqn = (string?)u.Element(Ns + "TestMethod")?.Attribute("className") + "."
                    + (string?)u.Element(Ns + "TestMethod")?.Attribute("name"),
            })
            .ToDictionary(x => x.Id, x => x.Fqn);

        var results = new List<TestResult>();

        foreach (var r in root.Descendants(Ns + "UnitTestResult"))
        {
            var executionId = (string?)r.Attribute("executionId") ?? "";
            var testName    = (string?)r.Attribute("testName") ?? "Unknown";
            var outcomeStr  = (string?)r.Attribute("outcome") ?? "NotExecuted";
            var duration    = ParseDuration((string?)r.Attribute("duration"));
            var fqn         = definitions.TryGetValue(executionId, out var d) ? d : testName;

            var output  = r.Element(Ns + "Output");
            var errorInfo = output?.Element(Ns + "ErrorInfo");

            var stdOut     = output?.Element(Ns + "StdOut")?.Value?.Trim();
            var stdErr     = output?.Element(Ns + "StdErr")?.Value?.Trim();
            var errorMsg   = errorInfo?.Element(Ns + "Message")?.Value?.Trim();
            var stackTrace = errorInfo?.Element(Ns + "StackTrace")?.Value?.Trim();

            results.Add(new TestResult(
                TestName:            testName,
                FullyQualifiedName:  fqn,
                Outcome:             ParseOutcome(outcomeStr),
                Duration:            duration,
                ErrorMessage:        string.IsNullOrEmpty(errorMsg) ? null : errorMsg,
                StackTrace:          string.IsNullOrEmpty(stackTrace) ? null : stackTrace,
                StdOut:              string.IsNullOrEmpty(stdOut) ? null : stdOut,
                StdErr:              string.IsNullOrEmpty(stdErr) ? null : stdErr
            ));
        }

        return results;
    }

    private static TestOutcome ParseOutcome(string value) => value.ToLowerInvariant() switch
    {
        "passed"      => TestOutcome.Passed,
        "failed"      => TestOutcome.Failed,
        "skipped"     => TestOutcome.Skipped,
        "notexecuted" => TestOutcome.NotExecuted,
        _             => TestOutcome.NotExecuted,
    };

    private static TimeSpan ParseDuration(string? value)
    {
        if (string.IsNullOrEmpty(value)) return TimeSpan.Zero;
        return TimeSpan.TryParse(value, out var ts) ? ts : TimeSpan.Zero;
    }

    private static string InferProjectName(string trxPath)
    {
        // Walk up to find a .csproj alongside the TRX, fall back to file name
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(trxPath));
            while (dir is not null)
            {
                var csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
                if (csproj is not null)
                    return Path.GetFileNameWithoutExtension(csproj);
                dir = Path.GetDirectoryName(dir);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return Path.GetFileNameWithoutExtension(trxPath);
    }
}
