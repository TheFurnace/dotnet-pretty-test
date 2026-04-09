namespace TestLens.Runner;

/// <summary>
/// Resolves user-supplied path arguments into a deduplicated list of .csproj absolute paths.
/// Accepts: .csproj files, .sln files (parsed to extract projects), and directories
/// (searched recursively for *.csproj).  When no inputs are given, defaults to the current
/// working directory.
/// </summary>
public static class ProjectDiscovery
{
    public static List<string> Resolve(IEnumerable<string> inputs)
    {
        var inputList = inputs.ToList();
        if (inputList.Count == 0)
            inputList.Add(Directory.GetCurrentDirectory());

        var projects = new List<string>();

        foreach (var input in inputList)
        {
            var full = Path.GetFullPath(input);

            if (File.Exists(full))
            {
                var ext = Path.GetExtension(full).ToLowerInvariant();
                if (ext == ".csproj")
                    projects.Add(full);
                else if (ext == ".sln")
                    projects.AddRange(ParseSolution(full));
                else
                    throw new InvalidOperationException(
                        $"Unsupported file type '{ext}'. Supply a .csproj, .sln, or directory.");
            }
            else if (Directory.Exists(full))
            {
                projects.AddRange(FindProjectsInDirectory(full));
            }
            else
            {
                throw new FileNotFoundException($"Path not found: {full}");
            }
        }

        // Deduplicate (case-insensitive on paths) while preserving order.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return projects.Where(p => seen.Add(p)).ToList();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IEnumerable<string> ParseSolution(string slnPath)
    {
        var slnDir = Path.GetDirectoryName(Path.GetFullPath(slnPath))!;

        foreach (var line in File.ReadAllLines(slnPath))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("Project(", StringComparison.Ordinal))
                continue;

            // Typical line:
            // Project("{FAE…}") = "Name", "path\to\Foo.csproj", "{GUID}"
            var parts = line.Split('"');
            if (parts.Length < 6) continue;

            var relPath = parts[5].Replace('\\', Path.DirectorySeparatorChar);
            if (!relPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                continue;

            var full = Path.GetFullPath(Path.Combine(slnDir, relPath));
            if (File.Exists(full))
                yield return full;
        }
    }

    private static IEnumerable<string> FindProjectsInDirectory(string dir) =>
        Directory.GetFiles(dir, "*.csproj", SearchOption.AllDirectories)
                 .OrderBy(p => p);
}
