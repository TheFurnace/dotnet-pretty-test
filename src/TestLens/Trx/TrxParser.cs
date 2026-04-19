using System.Xml.Linq;

namespace TestLens.Trx;

public static class TrxParser
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static ProjectRun Parse(string trxPath)
    {
        var results = ParseResults(trxPath, out var assemblyName);
        var projectName = assemblyName
            ?? Path.GetFileNameWithoutExtension(trxPath);
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
            return new ProjectRun(Path.GetFileNameWithoutExtension(trxPath), trxPath, []);
        }
    }

    /// <summary>
    /// Quickly extracts the assembly/project name from a TRX file without full parsing.
    /// Returns null if the name cannot be determined.
    /// </summary>
    public static string? GetAssemblyName(string trxPath)
    {
        try
        {
            XDocument doc;
            using (var stream = new FileStream(trxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                doc = XDocument.Load(stream);

            var root = doc.Root;
            if (root is null) return null;

            // Prefer TestMethod/@codeBase (preserves casing) over UnitTest/@storage.
            var path = root
                .Descendants(Ns + "UnitTest")
                .Select(u => (string?)u.Element(Ns + "TestMethod")?.Attribute("codeBase"))
                .FirstOrDefault(s => !string.IsNullOrEmpty(s));
            path ??= root
                .Descendants(Ns + "UnitTest")
                .Select(u => (string?)u.Attribute("storage"))
                .FirstOrDefault(s => !string.IsNullOrEmpty(s));

            if (path is not null)
                return Path.GetFileNameWithoutExtension(path);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static List<TestResult> ParseResults(string trxPath, out string? assemblyName)
    {
        XDocument doc;
        using (var stream = new FileStream(trxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            doc = XDocument.Load(stream);

        var root = doc.Root ?? throw new InvalidDataException("TRX file has no root element.");

        // Extract assembly name from the first UnitTest element.
        // Prefer the TestMethod/@codeBase attribute (preserves original casing)
        // over UnitTest/@storage (which is lowercased by vstest).
        assemblyName = root
            .Descendants(Ns + "UnitTest")
            .Select(u => (string?)u.Element(Ns + "TestMethod")?.Attribute("codeBase"))
            .FirstOrDefault(s => !string.IsNullOrEmpty(s));
        assemblyName ??= root
            .Descendants(Ns + "UnitTest")
            .Select(u => (string?)u.Attribute("storage"))
            .FirstOrDefault(s => !string.IsNullOrEmpty(s));
        if (assemblyName is not null)
            assemblyName = Path.GetFileNameWithoutExtension(assemblyName);

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
}
