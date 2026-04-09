# TestLens — Project Plan

_Drafted: 2026-04-07_

---

## Overview

**testlens** is a .NET global tool that wraps `dotnet test`, cleanly separates test output from runner noise, and provides three focused commands: a live pretty runner, a Markdown report generator, and a detailed error inspector.

Distributed as: `dotnet tool install -g testlens`

---

## Commands

### `testlens run [paths...] [options]`

Runs `dotnet test` on one or more targets (project files, solution files, or directories) and displays live progress + a summary table. Writes a TRX file for downstream use by `report` and `errors`.

**Arguments:**
- `paths` — zero or more paths to `.sln`, `.csproj`, or directories. Defaults to current directory (mirrors `dotnet test` behaviour).

**Options:**
- `--filter <expr>` — pass-through to `dotnet test --filter` (e.g. `--filter "FullyQualifiedName~MyTest"`)
- `--failed-from <file.trx>` — rerun only tests that failed in a previous TRX file (convenience wrapper around `--filter`)
- `--output-dir <dir>` — directory to write per-project TRX files (default: alongside each project file)
- `--parallel` — run build and test phases concurrently across projects (default: sequential)
- `--no-build` — pass-through to `dotnet test --no-build`
- `--configuration <config>` — pass-through (default: `Debug`)

**Terminal output:**
```
  Building solution...  ✓

  MyProject.Tests          ████████████████░░░░  16/20  3 failed
  MyOther.Tests            ████████████████████  42/42  ✓

  ┌─────────────────────────────────────────────────────┐
  │  Results                                            │
  ├──────────────────┬────────┬────────┬───────┬───────┤
  │ Project          │ Total  │ Passed │ Failed │ Skipped│
  ├──────────────────┼────────┼────────┼───────┼───────┤
  │ MyProject.Tests  │   20   │   17   │    3   │    0  │
  │ MyOther.Tests    │   42   │   42   │    0   │    0  │
  ├──────────────────┼────────┼────────┼───────┼───────┤
  │ Total            │   62   │   59   │    3   │    0  │
  └──────────────────┴────────┴────────┴───────┴───────┘

  3 failed tests — run `testlens errors testlens-results.trx` for details
```

No test stdout/stderr is shown inline. Elapsed time shown per project.

---

### `testlens report <file.trx> [options]`

Reads a TRX file and emits a Markdown report to stdout (pipe to a file or clipboard).

**Options:**
- `--output <file.md>` — write to file instead of stdout
- `--title <string>` — heading for the report (default: `Test Results`)

**Example output (Markdown):**

```markdown
## Test Results

| Project | Total | ✅ Passed | ❌ Failed | ⏭ Skipped |
|---------|-------|----------|----------|----------|
| MyProject.Tests | 20 | 17 | 3 | 0 |
| MyOther.Tests | 42 | 42 | 0 | 0 |
| **Total** | **62** | **59** | **3** | **0** |

<details>
<summary>❌ Failed tests (3)</summary>

**MyProject.Tests**
- `MyTest.ShouldDoThing` — Expected 42 but was 0
- `MyTest.ShouldHandleNull` — Object reference not set to an instance of an object
- `MyTest.EdgeCase` — Timeout after 5000ms

</details>
```

---

### `testlens errors <file.trx> [options]`

Reads a TRX file and displays detailed failure information in the terminal.

**Options:**
- `--filter <name>` — show only failures matching a substring of the test name
- `--show-output` — also print captured stdout/stderr from the test

**Terminal output:**
```
  ❌ 3 failed tests

  ── MyProject.Tests ──────────────────────────────────────

  FAIL  MyTest.ShouldDoThing
        Expected: 42
        Actual:    0
        at MyTest.ShouldDoThing() in MyTest.cs:line 42

  FAIL  MyTest.ShouldHandleNull
        System.NullReferenceException: Object reference not set...
        at MyProject.Service.Process() in Service.cs:line 17
        at MyTest.ShouldHandleNull() in MyTest.cs:line 58

  FAIL  MyTest.EdgeCase
        Timeout after 5000ms
```

---

## Architecture

```
testlens/
├── src/
│   └── TestLens/
│       ├── TestLens.csproj          # dotnet tool project
│       ├── Program.cs               # System.CommandLine root command wiring
│       ├── Commands/
│       │   ├── RunCommand.cs        # `run` — orchestrates dotnet test + live display
│       │   ├── ReportCommand.cs     # `report` — TRX → Markdown
│       │   └── ErrorsCommand.cs     # `errors` — TRX → pretty error display
│       ├── Runner/
│       │   ├── DotnetTestRunner.cs  # Launches dotnet test subprocess(es)
│       │   └── ProjectDiscovery.cs  # Finds projects/solutions from path args
│       ├── Trx/
│       │   ├── TrxParser.cs         # Deserialises TRX XML → domain model
│       │   └── TrxModels.cs         # TestRun, TestResult, etc.
│       └── Display/
│           ├── ProgressDisplay.cs   # Spectre.Console live progress bars
│           ├── SummaryTable.cs      # Spectre.Console summary table
│           ├── ErrorDisplay.cs      # Spectre.Console error formatting
│           └── MarkdownRenderer.cs  # Markdown string builder
└── tests/
    └── TestLens.Tests/
        └── TestLens.Tests.csproj
```

---

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `System.CommandLine` | CLI parsing, tab completion, help |
| `Spectre.Console` | Terminal rendering (tables, progress, markup) |
| *(BCL)* `System.Xml.Serialization` | TRX parsing (no extra dep needed) |

---

## How `run` Works Internally

1. **Discover targets** — resolve path arguments to a list of `.csproj` files (expanding `.sln` and directories).
2. **Build phase** — run `dotnet build <project> --no-restore` for each project. Each gets its own Spectre.Console progress row. Respects `--parallel`. Any build failure halts that project; others continue.
3. **Test phase** — for each successfully built project, launch `dotnet test <project> --no-build --logger "trx;LogFileName=<project-dir>/testlens.trx" --verbosity quiet`. Stdout/stderr captured and suppressed.
4. **Live display** — poll each project's TRX file every ~250ms; update per-project progress bars as tests complete.
5. **Summary** — on all projects completing, render the summary table. Print hint to run `testlens errors` if any failures.
6. With `--parallel`, build and test phases both run concurrently across projects using `Task.WhenAll`.

---

## TRX Polling vs. Real-Time

VSTest writes results to the TRX file as tests complete (not just at the end). Polling the file every ~250ms gives near-real-time progress without requiring a custom VSTest logger plugin. This keeps the tool self-contained — nothing needs to be installed or referenced in the target projects.

---

## Implementation Phases

### Phase 1 — Scaffold & TRX plumbing
- Create solution + project structure
- Add `System.CommandLine` + `Spectre.Console`
- Implement `TrxParser` + domain model
- Wire up `errors` command (pure TRX reading, no subprocess — fast to validate)

### Phase 2 — `run` command
- `ProjectDiscovery` (resolve path args)
- `DotnetTestRunner` (subprocess launch, stdout suppression)
- Live progress display with Spectre.Console
- Summary table on completion
- Single-project TRX merge (trivial for one project; solution for multi)

### Phase 3 — `report` command
- `MarkdownRenderer` producing GitHub-flavoured Markdown
- Stdout output + `--output` file option
- Collapsible `<details>` block for failures

### Phase 4 — Polish & distribution
- `--failed-from` flag (rerun failures)
- Tab completion (System.CommandLine built-in)
- `dotnet tool` packaging (`<PackAsTool>true</PackAsTool>`)
- README

---

## Decisions

1. **Multi-project parallelism** — sequential by default; opt-in via `--parallel` flag. All projects run concurrently when enabled; Spectre.Console live display handles multiple simultaneous progress bars.
2. **TRX output** — one TRX file per project, written to the same directory as the project file (e.g. `MyProject.Tests/testlens.trx`). The `--output-dir` option overrides the directory.
3. **Build step** — `run` performs an explicit `dotnet build` pass first (per project, respecting `--parallel`), each with its own progress indicator, before invoking `dotnet test --no-build`. This gives clean separation: build failures are reported before any tests attempt to run.

