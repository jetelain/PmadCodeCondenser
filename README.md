# Pmad.CodeCondenser

[![NuGet Version](https://img.shields.io/nuget/v/Pmad.CodeCondenser.svg)](https://www.nuget.org/packages/Pmad.CodeCondenser)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Pmad.CodeCondenser.svg)](https://www.nuget.org/packages/Pmad.CodeCondenser)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A static analysis developer utility and .NET tool built on **.NET 10** and Roslyn (`Microsoft.CodeAnalysis.CSharp`). It analyzes C# codebases to:

1. **Detect Structural Code Clones (Type-2 Clones)**: Identify methods sharing identical AST logic structures (ignoring variable, parameter, and constant renames) and provide actionable refactoring advice.
2. **Compact Repeated Inline Strings**: Detect string literals >= 100 characters used more than once, with estimated character/token savings, minimal constant declaration snippets, and typical usage patterns.
3. **Condense Verbose Comments**: Scan overly verbose regular comments (`//`, `/* */`) and XML documentation comments (`/// <summary>`, `<remarks>`, `<param>`) and condense them into clear, concise summaries using local or cloud LLMs via `Microsoft.Extensions.AI` and Ollama.
4. **WYSIWYG Guarantee & Incremental Snapshots**: Cache generated comment summaries and AST hashes so that dry-run reviews match applied code without LLM drift, while incremental runs only analyze changed code.
5. **CI/CD & AI Agent Guardrails**: Enforce strict duplication and repeated string tolerance in CI pipelines and AI coding agent workflows (exit code `2` on new violations).

---

## Prerequisites

- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** or higher.
- *(Optional)* **[Ollama](https://ollama.com/)**: Only needed for LLM comment summarization. Structural code clone detection is 100% standalone and requires no AI backend.
  - **Local Model (Default)**: Pull the recommended model:
    ```bash
    ollama pull gemma4:e4b
    ```
  - **Cloud Model (Zero local VRAM)**: Log in to Ollama Cloud with free tier quota:
    ```bash
    ollama login
    ```

---

## Installation

`Pmad.CodeCondenser` is distributed as a .NET Tool package on [NuGet.org](https://www.nuget.org/packages/Pmad.CodeCondenser).

### 1. Install as a .NET Global Tool (Recommended)

Install globally to make the `codecondenser` command available everywhere on your machine:

```bash
dotnet tool install -g Pmad.CodeCondenser --prerelease
```

To update an existing global installation to the latest version:

```bash
dotnet tool update -g Pmad.CodeCondenser --prerelease
```

> [!NOTE]
> Make sure your .NET tools directory is in your system's `PATH`:
> - **Windows**: `%USERPROFILE%\.dotnet\tools`
> - **Linux / macOS**: `$HOME/.dotnet/tools`

### 2. Install as a Local Tool (Repository / Team Manifest)

For team projects and automated CI pipelines, install `Pmad.CodeCondenser` as a repository-scoped local tool:

```bash
# 1. Create a tool manifest in your repository root (if not already present):
dotnet new tool-manifest

# 2. Add Pmad.CodeCondenser to the manifest:
dotnet tool install Pmad.CodeCondenser --prerelease
```

Commit the generated `.config/dotnet-tools.json` file. Team members and CI runners can restore and invoke the tool with:

```bash
# Restore tools:
dotnet tool restore

# Run via dotnet:
dotnet codecondenser [path] [options]
# or:
dotnet tool run codecondenser [path] [options]
```

### 3. Run Directly from Source

You can also run directly from the cloned repository without installing:

```bash
dotnet run --project src/Pmad.CodeCondenser.Cli -- [path] [options]
```

---

## Quick Start & Core Workflows

The tool binary command is **`codecondenser`** (or `dotnet codecondenser` when used as a local tool).

```bash
codecondenser [command] [path] [options]
```

### Scenario A: Clone Detection Only (Fast Dry-Run)

Scan for structural code clones without checking comments:

```bash
codecondenser analyze ./src --no-comments
```

Roslyn parses the syntax trees, normalizes identifier names, statements, and literals, and prints all duplicate method clusters with location details and refactoring guidance.

### Scenario B: CI / AI Agent-Friendly Status Report (Report Mode - No LLM Required)

When no LLM is configured (or with `codecondenser report`), CodeCondenser runs deterministic status reporting:

```bash
codecondenser report ./src
```

Report mode scans both structural code clones and overly verbose comments, printing actionable recommendations and suppression tips (e.g. `condense="no"` or `// codecondenser:ignore`) without needing any local or cloud LLM backend.

### Scenario C: LLM Comment Summarization (Analyze Mode)

When an LLM model or endpoint is specified, CodeCondenser summarizes verbose comments:

```bash
codecondenser analyze ./src -m gemma4:e4b
# Or explicitly:
codecondenser analyze ./src --summary summarize -m gemma4:e4b
```

Overly verbose comments are condensed using the LLM, and results are safely stored in `.codecondenser/snapshot.json` without modifying any files on disk.

### Scenario D: Apply Summarized Comments to Source Code

Apply the summarized comments directly into your C# source files on disk:

```bash
codecondenser apply ./src
```

> [!TIP]
> **WYSIWYG Guarantee**: `apply` reuses the exact LLM summaries generated and cached during dry-run analysis. No second LLM call is made, guaranteeing zero drift between review and commit.

### Scenario E: Incremental Analysis (Modified Files Only)

Analyze only files that were added or modified since the last snapshot baseline:

```bash
codecondenser analyze ./src --incremental
```

In incremental mode, if new duplication or verbose comments are detected, CodeCondenser outputs unified **actionable feedback only once at the end**, complete with suppression instructions (`condense="no"`, `// codecondenser:ignore`, or `[SuppressMessage]`).

### Scenario F: Refresh Snapshot Baseline

Update or create the baseline snapshot stored in `.codecondenser/snapshot.json`:

```bash
codecondenser snapshot ./src
```

### Scenario G: Install or Update Workspace AI Agent Skills

Install or synchronize the bundled AI agent skills into a workspace's `.agents/skills` directory so AI coding agents understand how to use `Pmad.CodeCondenser`:

```bash
# Install or update skills in the current workspace:
codecondenser skills

# Target a specific workspace:
codecondenser skills ./my-project

# Preview what files would be created or updated without modifying disk:
codecondenser skills --dry-run
```

---

## CI/CD & Agent Guardrail Integration

`Pmad.CodeCondenser` can act as an automated gatekeeper in Pull Requests and CI workflows to block duplicate code introduction or unchecked verbose comments.

When executed with `--incremental --duplication-tolerance 0` (and optionally `--comment-tolerance 0`), the tool compares current changes against the baseline snapshot. If new code clones or verbose comments are introduced, it outputs structured, actionable instructions once at the end and exits with **exit code `2`**, failing the build with clear remediation and suppression steps.

### GitHub Actions Workflow Example

```yaml
name: Static Analysis & Clone Gate

on:
  pull_request:
    branches: [main]

permissions:
  contents: read
  security-events: write

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET 10 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install Pmad.CodeCondenser
        run: dotnet tool install -g Pmad.CodeCondenser --prerelease

      - name: Run Incremental Clone Check with SARIF Export
        id: analyze
        continue-on-error: true
        run: |
          codecondenser ./src \
            --incremental \
            --duplication-tolerance 0 \
            --no-comments \
            --format sarif \
            --output results.sarif

      - name: Upload SARIF to GitHub Code Scanning
        uses: github/codeql-action/upload-sarif@v3
        if: always() && github.event.pull_request.head.repo.full_name == github.repository
        with:
          sarif_file: results.sarif
          category: codecondenser

      - name: Enforce Policy Quality Gate
        if: steps.analyze.outcome == 'failure'
        run: |
          echo "::error::Pmad.CodeCondenser detected code duplication exceeding tolerance."
          exit 1
```

---

## Command-Line Reference

### Commands

| Command | Description |
| :--- | :--- |
| `analyze [path]` | Analyze C# codebase in dry-run mode *(default)* |
| `apply [path]` | Write summarized comments directly to source files on disk |
| `snapshot [path]` | Create or refresh the baseline snapshot |
| `skills [path]` | Install or update bundled AI agent skills in the workspace (`.agents/skills`) |

### Options & Flags

| Option | Alias | Description | Default |
| :--- | :---: | :--- | :--- |
| `[path]` | | Target directory or `.cs` file to analyze | `.` *(current directory)* |
| `-a, --apply` | `-a` | Write shortened comments directly to source files on disk | `false` |
| `-i, --incremental` | `-i` | Use snapshot baseline to analyze only new/modified files | `false` |
| `-u, --update-snapshot` | `-u` | Refresh the snapshot baseline | `false` |
| `-dt, --duplication-tolerance <n>` | `-dt` | Maximum new clone clusters tolerated in incremental mode | `0` |
| `-ct, --comment-tolerance <n>` | `-ct` | Maximum new verbose comments tolerated in incremental mode | `null` |
| `-st, --string-tolerance <n>` | `-st` | Maximum new repeated string groups tolerated in incremental mode | `null` |
| `--summary <mode>` | | Comment summary mode: `disabled`, `summarize`, or `report` | Auto (`summarize` if LLM configured, `report` otherwise) |
| `--report-only` | | Report-only mode for comments without calling an LLM (alias for `--summary report`) | `false` |
| `--include <pattern>` | | Add glob pattern of files/directories to include (multi-value) | `[]` *(all .cs files)* |
| `--exclude <pattern>` | | Add glob pattern of files/directories to exclude (multi-value) | `[]` |
| `--exclude-tests` | `--production-only` | Completely exclude unit and integration test files | `false` |
| `--enforce-test-duplication` | | Treat test clones as CI policy violation in tolerance gate | `false` |
| `--include-generated` | | Disable automatic exclusion of generated files | `false` |
| `--no-comments` | | Disable comment extraction and summarization (alias for `--summary disabled`) | `false` |
| `--enable-comments` | | Explicitly enable comment extraction and summarization | `true` |
| `--no-duplication` | | Disable structural code clone detection | `false` |
| `--enable-duplication` | | Explicitly enable structural code clone detection | `true` |
| `--no-strings` | `--disable-strings` | Disable repeated inline string detection | `false` |
| `--enable-strings` | | Explicitly enable repeated inline string detection | `true` |
| `-ts, --threshold-statements <n>` | `-ts` | Minimum statement count to analyze a method for clones | `8` |
| `-tstr, --threshold-strings <n>` | `-tstr` | Minimum character length for repeated string detection | `100` |
| `-so, --string-occurrences <n>` | `-so` | Minimum occurrences for repeated string detection | `2` |
| `-tc, --threshold-comments <n>` | `-tc` | Minimum character length for comments to be flagged as verbose | `100` |
| `-txs, --threshold-xml-summary <n>` | `-txs` | Minimum content length for XML `<summary>` / `<remarks>` tags | `threshold-comments` (`100`) |
| `-txp, --threshold-xml-param <n>` | `-txp` | Minimum content length for XML `<param>` tags | `threshold-comments / 2` (`50`) |
| `-sc, --summarized-comments <path>` | `-sc` | Path to summarized comments JSON file stored in git | `.codecondenser/summarized-comments.json` |
| `-mll, --max-line-length <n>` | `-mll` | Maximum line length for formatted comments | `120` |
| `-m, --model <name>` | `-m` | LLM model tag for comment summarization | `gemma4:e4b` |
| `-e, --endpoint <url>` | `-e` | Ollama endpoint URL | `http://localhost:11434` |
| `-cs, --context-size <n>` | `-cs` | Ollama context window size (`num_ctx`) | `24576` |
| `-c, --config <path>` | `-c` | Explicit path to `codecondenser.json` configuration file | Auto-detected |
| `-gc, --global-config <path>` | `-gc` | Explicit path to custom global user profile configuration file | Auto-detected (`%APPDATA%\CodeCondenser\codecondenser.json` on Windows, `~/.codecondenser/codecondenser.json` on Linux/macOS) |
| `--no-global-config` | | Disable loading of global user profile configuration file | `false` |
| `-n, --dry-run` | `-n` | Preview actions without writing changes to disk | `false` |
| `--force` | | Force overwrite skill files even if up to date | `false` |
| `--clean` | | Remove obsolete files from skill directories | `false` |
| `--skill <name>` | | Specify a single skill to install/update (default: all) | `null` |
| `--skills-dir <path>` | | Override target skills directory (default: `<workspace>/.agents/skills`) | `null` |
| `-f, --format <type>` | `-f` | Output report format: `console`, `sarif`, `json` | `console` |
| `-o, --output <path>` | `-o` | File path to write report output to instead of stdout | `null` *(stdout)* |
| `--no-color` | | Disable ANSI color formatting in console output (auto-disabled if redirected or `NO_COLOR` is set) | `false` |
| `-h, --help` | | Show usage instructions and command help | |
| `-v, --version` | | Show tool version information | |

### Exit Codes

| Exit Code | Name | Meaning |
| :---: | :--- | :--- |
| `0` | **Success** | Analysis clean; no policy violations. |
| `1` | **General Error** | Invalid arguments, missing file, or unhandled exception. |
| `2` | **Policy Violation** | In incremental mode, newly introduced code clones exceed `duplicationTolerance`, new verbose comments exceed `commentTolerance`, or new repeated strings exceed `repeatedStringTolerance`. Outputs refactoring and suppression advice for developers and AI agents. |
| `130` | **Canceled** | Execution canceled by user (`Ctrl+C`). |

---

## CI/CD, SARIF & Machine-Readable Formats

`Pmad.CodeCondenser` supports standard **SARIF 2.1.0** and **structured JSON** exports for automated CI/CD integration, PR annotations, and custom quality dashboards:

### 1. SARIF 2.1.0 Export (`--format sarif`)

Generates standard OASIS SARIF 2.1.0 compatible with GitHub Code Scanning, GitLab, Azure DevOps, and VS Code.

- **Symmetrical PR Diff Annotations**: Emits a `result` for each participating method in a clone cluster with other methods in `relatedLocations`, guaranteeing that pull requests modifying any duplicate file receive inline PR annotations.
- **Alert De-duplication**: Includes stable `partialFingerprints` (`astHash/v1` and `locationKey/v1`) and a deterministic Version 5 UUID `correlationGuid` to group identical clones on GitHub's Security tab without multiplying alerts.
- **Rule Taxonomy**:
  - `CC001` (`warning`): `ProductionCodeDuplication`
  - `CC002` (`note`): `TestCodeDuplication`
  - `CC003` (`note`): `VerboseComment`
  - `CC004` (`warning`): `CrossBoundaryDuplication`
  - `CC005` (`warning`): `SubMethodCodeDuplication`
  - `CC006` (`warning`): `RepeatedInlineString`

```bash
# Export SARIF to file
codecondenser analyze ./src --format sarif --output results.sarif

# Or pipe directly (progress/banners are automatically routed to stderr)
codecondenser analyze ./src --format sarif > results.sarif
```

### 2. Structured JSON Export (`--format json`)

Outputs a versioned JSON report conforming to the [`schemas/codecondenser-report.json`](schemas/codecondenser-report.json) schema, including full metrics, cluster impact scores, AST hashes, line numbers, and actionable recommendations.

```bash
codecondenser analyze ./src --format json --output results.json
```

---

## Suppression Markers & Persistent Storage

### 1. XML Documentation Formatting (120 Columns & Summary Tags)

- **Missing `<summary>` Tags**: XML documentation comments missing `<summary>` tags automatically have them added.
- **120 Columns Line Length**: Comments are formatted and word-wrapped so that every line stays within 120 columns (including indentation and comment prefixes), while preserving inline tags (e.g. `<see cref="..."/>`, `<c>...</c>`).

### 2. Persistent Summarized Comments (`.codecondenser/summarized-comments.json`)

Even summarized comments might exceed the character threshold (e.g. 110 characters). To ensure they are not re-analyzed or flagged across new sessions or clean checkouts:
- Applied summaries are saved to `.codecondenser/summarized-comments.json`, which should be **committed to Git**.
- `.codecondenser/snapshot.json` remains scoped to the local agent/developer session and should be gitignored:

```gitignore
# CodeCondenser: ignore session-scoped snapshot cache, commit summarized comments store
/.codecondenser/snapshot.json
```

### 3. Suppression & Ignore Markers

- **Ignore a Regular Comment**:
  Use `// codecondenser:ignore` or `// nocondense` on the line preceding the comment, or embed `codecondenser:ignore` / `nocondense` inside the comment.
- **Ignore an XML Documentation Comment**:
  Add `condense="no"` (or `condense="false"`, `nocondense="true"`, `nocondense`) as an attribute on any XML tag, e.g.:
  - `<summary condense="no">...</summary>`
  - `<summary condense="false">...</summary>`
  - `<summary nocondense="true">...</summary>`
  - `<remarks condense="no">...</remarks>`
- **Ignore Code Duplication on a Method**:
  - Comment above or inside method: `// codecondenser:ignore-duplicate` or `// codecondenser:ignore`
  - Attribute on method: `[SuppressMessage("CodeCondenser", "Duplication")]` or `[CodeCondenserIgnore]`
- **Ignore a Repeated Inline String**:
  - Comment on the line preceding or inline: `// codecondenser:ignore-string` or `// nocondense`
  - Attribute on enclosing member or class: `[SuppressMessage("CodeCondenser", "RepeatedString")]`
- **Standalone Ignore File (`.codecondenserignore`)**:
  Add glob patterns (like `.gitignore`) to a `.codecondenserignore` file in the repository root to exclude directories or files without modifying `codecondenser.json`.

---

## Configuration (`codecondenser.json`)

`Pmad.CodeCondenser` automatically looks for a `codecondenser.json` configuration file in the target directory or working directory:

```json
{
  "include": [
    "src/**"
  ],
  "exclude": [
    "**/Migrations/**",
    "**/bin/**",
    "**/obj/**"
  ],
  "useDefaultExcludes": true,
  "excludeTests": false,
  "enforceTestDuplication": false,
  "testPaths": [
    "tests/**",
    "test/**",
    "**/*.Tests/**",
    "**/*.Test/**",
    "**/*Test.cs",
    "**/*Tests.cs"
  ],
  "excludeGeneratedCode": true,
  "generatedCodePatterns": [
    "**/*.Designer.cs",
    "**/*.g.cs",
    "**/*.generated.cs",
    "**/*ModelSnapshot.cs",
    "**/Migrations/**"
  ],
  "summaryMode": "Report",
  "enableCommentSummarization": true,
  "enableDuplicationDetection": true,
  "enableRepeatedStringDetection": true,
  "duplicationTolerance": 0,
  "duplicationExitCode": 2,
  "commentTolerance": null,
  "commentExitCode": 2,
  "repeatedStringTolerance": null,
  "repeatedStringExitCode": 2,
  "repeatedStringLengthThreshold": 100,
  "repeatedStringMinOccurrences": 2,
  "commentLengthThreshold": 100,
  "xmlSummaryLengthThreshold": 100,
  "xmlParamLengthThreshold": 50,
  "maxCommentLineLength": 120,
  "duplicationStatementThreshold": 8,
  "includeXmlDocumentation": true,
  "maxParallelLlmRequests": 4,
  "provider": "Ollama",
  "ollama": {
    "endpoint": "http://localhost:11434",
    "model": "gemma4:e4b",
    "contextSize": 24576
  },
  "snapshotFilePath": ".codecondenser/snapshot.json",
  "summarizedCommentsFilePath": ".codecondenser/summarized-comments.json"
}
```

CLI options override settings from `codecondenser.json`.

### Global User Profile Configuration

To avoid committing personal LLM endpoints, local ports, or credentials into shared repository configuration files, `Pmad.CodeCondenser` supports a **global user profile configuration file**:

- **Location**:
  - **Windows**: `%APPDATA%\CodeCondenser\codecondenser.json` or `%APPDATA%\CodeCondenser\config.json` (fallback: `%USERPROFILE%\.codecondenser\codecondenser.json`)
  - **Linux / macOS**: `~/.codecondenser/codecondenser.json` or `~/.codecondenser/config.json` (fallback: `~/.codecondenser.json`)
- **Environment Variable Override**: `CODECONDENSER_GLOBAL_CONFIG` or `PMAD_CODECONDENSER_GLOBAL_CONFIG` (or directory override via `CODECONDENSER_APPDATA` / `CODECONDENSER_USER_PROFILE`)
- **CLI Flags**: `--global-config <path>` to specify a custom path, or `--no-global-config` to bypass global profile loading entirely.

#### Hierarchical Precedence
Settings are resolved in the following priority order (highest to lowest):
1. **CLI Arguments** (`--model`, `--endpoint`, `--summary`, etc.)
2. **Project Configuration** (`codecondenser.json` in the target or repository root)
3. **Global User Profile Configuration** (`%APPDATA%\CodeCondenser\codecondenser.json` on Windows, `~/.codecondenser/codecondenser.json` on Linux/macOS)
4. **Built-in Defaults**

#### Example Global Config (`codecondenser.json`)
```json
{
  "provider": "Ollama",
  "ollama": {
    "endpoint": "http://localhost:11434",
    "model": "gemma4:e4b",
    "contextSize": 24576
  },
  "maxParallelLlmRequests": 4
}
```

> [!NOTE]
> **Summary Mode Safety**:
> When an LLM model or endpoint is configured in your **global user profile**, `codecondenser` continues to default to fast, dry-run **`report` mode**.
> This prevents accidental, slow LLM calls on every execution across your repositories.
> To run LLM summarization using your global configuration, explicitly pass `--summary summarize` (or `-m <model>`).

---

## Comment Summarization Benchmark & Model Selection

To identify optimal models for comment condensation, a comprehensive benchmark was conducted evaluating local and cloud Ollama candidates against comments extracted from reference projects (`ComicGenCLI`, `NightCodeForge`, `HugeImages`, `Pmad.Git`), evaluated by **`Qwen3.6-35B-A3B:latest`** (Temperature: `0.0`, strict multi-criteria JSON scoring):

| Rank | Model | Type | Overall Score | Avg Latency | Median Latency | Throughput | Compression | Recommendation |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| 🥇 | **`gemma4:e4b`** | Local | **9.92 / 10** | 464 ms | **198 ms** | 205.8 tok/s | **20.9%** | 🏆 **Default / Top Local Quality** (near-perfect technical fidelity & conciseness) |
| 🥇 | **`gpt-oss:20b-cloud`** | Cloud | **9.92 / 10** | 2,313 ms | 1,807 ms | 88.0 tok/s | **21.4%** | ☁️ **Top Cloud Quality** (highest accuracy 9.88/10, zero local VRAM, free tier) |
| 🥈 | **`jetelain/Qwen3.5-4B:latest`** | Local | **9.90 / 10** | 365 ms | 212 ms | 214.3 tok/s | 16.4% | ⚖️ **Best Balanced Local** (ultra-fast latency with `think: false`) |
| 🥉 | **`qwen3.5:4b`** | Local | **9.84 / 10** | 429 ms | 236 ms | 142.8 tok/s | 20.0% | Strong XML doc summaries |
| 4 | **`gemma4:e2b`** | Local | **9.82 / 10** | 551 ms | **126 ms** | **309.3 tok/s** | 19.7% | Highest raw generation speed |
| 5 | **`nemotron-3-super:cloud`** | Cloud | **9.78 / 10** | 1,307 ms | 654 ms | 45.1 tok/s | 15.0% | Fast cloud alternative (120B MoE) |
| 6 | **`nemotron-3-nano:30b-cloud`** | Cloud | **9.68 / 10** | **424 ms** | 413 ms | 78.5 tok/s | 19.8% | ⚡ **Fastest & Lightest Cloud** (sub-second, free tier, zero local VRAM) |
| 7 | **`qwen3.5:2b`** | Local | **9.54 / 10** | **342 ms** | 176 ms | 191.9 tok/s | 13.6% | ⚡ **Fastest Local** (minimal VRAM footprint) |

### Model Recommendations by Environment

- **Dedicated GPU (>= 8–12 GB VRAM):** Use **`gemma4:e4b`** (default). 100% offline, 200+ tok/s, top-tier fidelity.
- **Limited VRAM / Running in Parallel with Primary Coding LLM (Cloud Models):**
  - **Highest Quality:** Use **`gpt-oss:20b-cloud`** (`-m gpt-oss:20b-cloud`). Ties with `gemma4:e4b` at 9.92/10, highest accuracy (9.88/10), and uses 0 GB local VRAM.
  - **Fastest Cloud:** Use **`nemotron-3-nano:30b-cloud`** (`-m nemotron-3-nano:30b-cloud`). Sub-second response (424 ms average), 9.68/10 score, and uses 0 GB local VRAM.

> [!TIP]
> **Free Cloud Tier**: Cloud models (`gpt-oss:20b-cloud`, `nemotron-3-nano:30b-cloud`, `nemotron-3-super:cloud`) are available through Ollama Cloud with an included free tier accommodating up to ~50,000 comment summaries. Create an account on [ollama.com](https://ollama.com) and run:
> ```bash
> ollama login
> ```

*For full benchmark methodology, CLI runner, and qualitative side-by-side case studies, see [src/Pmad.CodeCondenser.Benchmark/README.md](src/Pmad.CodeCondenser.Benchmark/README.md).*

---

## Architecture

- **`Pmad.CodeCondenser.Core`**: Domain models, AST normalization (`AstNormalizer`), clone detection (`CloneDetector`), comment extraction (`VerboseCommentExtractor`), Roslyn trivia rewriting (`VerboseCommentRewriter`), incremental diffing (`SnapshotDiffer`), and analysis engine (`CodeCondenserEngine`).
- **`Pmad.CodeCondenser.Infrastructure`**: LLM integration via `Microsoft.Extensions.AI` and `OllamaSharp` (`CommentSummarizer`), and snapshot JSON storage (`JsonSnapshotStore`).
- **`Pmad.CodeCondenser.Cli`**: Command-line interface with color-coded formatting and packaging as a packable .NET tool (`codecondenser`).

---

## License

MIT License. See [LICENSE](LICENSE) for details.

