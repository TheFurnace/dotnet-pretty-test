# Research: Existing UIs/TUIs for `dotnet test`

_Generated: 2026-04-07_

---

## Existing Tools & Approaches

### Loggers built into `dotnet test` / VSTest

`dotnet test` supports pluggable loggers via `--logger`:

| Logger | Notes |
|--------|-------|
| `console` | Default. Mixes test stdout/stderr with runner output. Verbosity levels: `quiet`, `minimal`, `normal`, `detailed`, `diagnostic` |
| `trx` | Generates a VS Test Results XML file. Machine-readable. Captures test stdout separately. |
| `html` | Generates an HTML report (basic). |
| `junit` | Community logger (e.g. `JunitXml.TestLogger` NuGet). Useful for CI. |
| `GitHubActions` | `GitHubActionsTestLogger` NuGet — emits GitHub Actions workflow commands for inline PR annotations. |

**Key insight:** `--logger trx` separates test stdout/stderr from runner output inside the XML. This is the cleanest way to get structured, separated output.

### Third-Party CLI Tools

| Tool | Description | Status |
|------|-------------|--------|
| `dotnet-reportgenerator` | Generates coverage HTML reports from Cobertura/OpenCover/etc. Not test results per se. | Active |
| `trx-to-markdown` / similar | Converts TRX XML → Markdown table. Several one-off scripts/gists exist; no dominant package. | Scattered |
| `dotnet-test-rerun` | Reruns failed tests automatically. Parses TRX to find failures. | Active |
| `Csharpier` | Code formatter, unrelated but shows dotnet tool packaging patterns. | — |

### TUI/UI Libraries for .NET

| Library | Description |
|---------|-------------|
| **Spectre.Console** | Rich console output: tables, progress bars, trees, live displays, markup. The de-facto standard for pretty .NET CLI output. MIT licensed. |
| **Terminal.Gui** | Full TUI framework (windows, dialogs, widgets). More like ncurses. Overkill for pretty output. |

### Test Framework Native Reporters

- **xUnit**: Has `IRunnerReporter` extension points. Default output is minimal.
- **NUnit**: Has console runner (`nunit3-console`) with its own formatting, separate from `dotnet test`.
- **MSTest**: No notable custom reporters.
- **TUnit**: Newer framework, has nicer built-in output but still subject to stdout mixing.

### GitHub Actions / CI Integration

- `GitHubActionsTestLogger` NuGet package emits `::error::` / `::warning::` annotations inline.
- TRX files can be uploaded as artifacts and displayed in some GitHub Actions dashboards (third-party actions).
- No native GitHub PR markdown table for test results exists out-of-the-box.

---

## Key Technical Insights

### The stdout mixing problem

When tests call `Console.WriteLine(...)`, that output goes to the process stdout alongside the VSTest runner output. There's no built-in separator in the default console logger.

**Solution:** Run with `--logger trx` (or a custom VSTest logger). The TRX XML format stores `<Output><StdOut>` per test, fully separated from runner output. The runner then gets full control of what to display on the terminal.

### Two architectural paths

#### Path A: Post-run TRX parser
1. Run `dotnet test --logger "trx;LogFileName=results.trx" --verbosity quiet`
2. Suppress noisy stdout by piping/discarding it
3. Parse the TRX file after completion
4. Render pretty output (Markdown table, Spectre.Console summary, etc.)

**Pros:** Simple. Clean separation. Easy to generate both terminal and Markdown output.  
**Cons:** No real-time progress — you wait for all tests to finish before seeing anything.

#### Path B: Custom VSTest logger (NuGet package)
Implement `Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.ITestLogger`. Gets callbacks per test result in real time.

**Pros:** Real-time progress as each test finishes.  
**Cons:** Significant complexity; must be distributed as a NuGet logger package and referenced per project/solution. Harder to use as a standalone dotnet tool.

#### Path C: Hybrid — stream TRX + watch file
Run with TRX logger, poll/tail the TRX file (or a temp file) while tests run, update a live Spectre.Console display. Approximates real-time without needing a custom logger.

---

## Gaps / Opportunities

1. **No dominant "pretty output" tool exists** for `dotnet test` as a standalone dotnet global tool. The space is open.
2. **Markdown report generation** from test results for PR descriptions is entirely unserved by good tooling.
3. **Progress tracking** (X/N tests passed, spinner per project) is not offered by any existing tool in a clean way.
4. **Multi-project/solution** handling with aggregate summaries is absent from existing tools.

---

## Recommended Stack (preliminary)

- **Runtime:** .NET global tool (`dotnet tool install -g`)
- **Output library:** Spectre.Console (tables, progress, live rendering)
- **Test result ingestion:** TRX logger (built-in, no extra dependencies in target projects)
- **Markdown output:** Manual render or Markdig (for converting a Markdown string)

