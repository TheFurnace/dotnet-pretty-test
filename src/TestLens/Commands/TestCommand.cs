using System.CommandLine;

namespace TestLens.Commands;

/// <summary>
/// Provides the System.CommandLine definition for `testlens test` so that
/// `testlens --help` shows it in the command list.  Actual execution is
/// intercepted in Program.cs before System.CommandLine sees it.
/// </summary>
public static class TestCommand
{
    public static Command Build()
    {
        var cmd = new Command("test",
            "Run dotnet test with pretty output. All arguments are forwarded to dotnet test.\n\n" +
            "Examples:\n" +
            "  testlens test\n" +
            "  testlens test MyProject.sln\n" +
            "  testlens test --filter \"FullyQualifiedName~MyTest\"\n" +
            "  testlens test -c Release --no-build\n" +
            "  testlens test path/to/Tests.csproj --filter Category=Unit");

        cmd.IsHidden = false;

        // We don't actually handle invocation here — Program.cs intercepts it.
        // But we set a handler so System.CommandLine doesn't complain.
        cmd.SetHandler(() =>
        {
            // Should never be reached — Program.cs intercepts "test" first.
        });

        return cmd;
    }
}
