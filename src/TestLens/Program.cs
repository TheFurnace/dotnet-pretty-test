using System.CommandLine;
using TestLens.Commands;

// ── Intercept "test" subcommand for raw arg passthrough ───────────────────────
// testlens test needs to forward all arguments verbatim to `dotnet test`,
// so we bypass System.CommandLine for it and handle it directly.

if (args.Length > 0 && args[0] == "test")
{
    var passthrough = args.AsSpan(1).ToArray();
    return await TestLens.Runner.TestHandler.ExecuteAsync(passthrough);
}

// ── Everything else goes through System.CommandLine ───────────────────────────

var root = new RootCommand("testlens — pretty test runner and reporter for dotnet test")
{
    ErrorsCommand.Build(),
    TestCommand.Build(),
};

return await root.InvokeAsync(args);
