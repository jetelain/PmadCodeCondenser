# Pmad.CodeCondenser

[![NuGet Version](https://img.shields.io/nuget/v/Pmad.CodeCondenser.svg)](https://www.nuget.org/packages/Pmad.CodeCondenser)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Pmad.CodeCondenser.svg)](https://www.nuget.org/packages/Pmad.CodeCondenser)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A static analysis developer utility and .NET tool built on **.NET 10** and Roslyn (`Microsoft.CodeAnalysis.CSharp`). It analyzes C# codebases to:

1. **Detect Structural Code Clones (Type-2 Clones)**: Identify methods sharing identical AST logic structures (ignoring variable, parameter, and constant renames) and provide actionable refactoring advice.
2. **Condense Verbose Comments**: Scan overly verbose regular comments (`//`, `/* */`) and XML documentation comments (`/// <summary>`, `<remarks>`, `<param>`) and condense them into clear, concise summaries using local or cloud LLMs via `Microsoft.Extensions.AI` and Ollama.
3. **WYSIWYG Guarantee & Incremental Snapshots**: Cache generated comment summaries and AST hashes so that dry-run reviews match applied code without LLM drift, while incremental runs only analyze changed code.
4. **CI/CD & AI Agent Guardrails**: Enforce strict duplication tolerance in CI pipelines and AI coding agent workflows (exit code `2` on new duplication).

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

Install globally to make the `pmad-codecondenser` command available everywhere on your machine:

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
dotnet pmad-codecondenser [path] [options]
# or:
dotnet tool run pmad-codecondenser [path] [options]
```

### 3. Run Directly from Source

You can also run directly from the cloned repository without installing:

```bash
dotnet run --project src/Pmad.CodeCondenser.Cli -- [path] [options]
```

---

## Quick Start & Core Workflows

The tool binary command is **`pmad-codecondenser`** (or `dotnet pmad-codecondenser` when used as a local tool).

```bash
pmad-codecondenser [command] [path] [options]
```

### Scenario A: Instant Code Clone Detection (No AI / Ollama Required)

Scan for structural code clones without connecting to any LLM:

```bash
pmad-codecondenser ./src --no-comments
```

Roslyn parses the syntax trees, normalizes identifier names, statements, and literals, and prints all duplicate method clusters with location details and refactoring guidance.

### Scenario B: Dry-Run Analysis (Clones + Proposed Comment Summaries)

Perform a full dry-run analysis. Overly verbose comments are condensed using the LLM, and results are safely stored in `.codecondenser/snapshot.json` without modifying any files on disk:

```bash
pmad-codecondenser ./src
```

### Scenario C: Apply Summarized Comments to Source Code

Apply the summarized comments directly into your C# source files on disk:

```bash
# Using the --apply flag:
pmad-codecondenser ./src --apply

# Or using the 'apply' command verb:
pmad-codecondenser apply ./src
```

> [!TIP]
> **WYSIWYG Guarantee**: `apply` reuses the exact LLM summaries generated and cached during dry-run analysis. No second LLM call is made, guaranteeing zero drift between review and commit.

### Scenario D: Incremental Analysis (Modified Files Only)

Analyze only files that were added or modified since the last snapshot baseline:

```bash
pmad-codecondenser ./src --incremental
```

### Scenario E: Refresh Snapshot Baseline

Update or create the baseline snapshot stored in `.codecondenser/snapshot.json`:

```bash
# Using the snapshot command verb:
pmad-codecondenser snapshot ./src

# Or using the --update-snapshot flag:
pmad-codecondenser ./src --update-snapshot
```

---

## CI/CD & Agent Guardrail Integration

`Pmad.CodeCondenser` can act as an automated gatekeeper in Pull Requests and CI workflows to block duplicate code introduction.

When executed with `--incremental --duplication-tolerance 0`, the tool compares current changes against the baseline. If new code clones are introduced, it outputs structured, actionable instructions and exits with **exit code `2`**, failing the build.

### GitHub Actions Workflow Example

```yaml
name: Code Quality & Duplication Check

on:
  pull_request:
    branches: [main]

jobs:
  check-clones:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET 10 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install Pmad.CodeCondenser
        run: dotnet tool install -g Pmad.CodeCondenser --prerelease

      - name: Run Incremental Clone Check
        run: pmad-codecondenser ./src --incremental --duplication-tolerance 0 --no-comments
```

---

## Command-Line Reference

### Commands

| Command | Description |
| :--- | :--- |
| `analyze [path]` | Analyze C# codebase in dry-run mode *(default)* |
| `apply [path]` | Write summarized comments directly to source files on disk |
| `snapshot [path]` | Create or refresh the baseline snapshot |

### Options & Flags

| Option | Alias | Description | Default |
| :--- | :---: | :--- | :--- |
| `[path]` | | Target directory or `.cs` file to analyze | `.` *(current directory)* |
| `-a, --apply` | `-a` | Write shortened comments directly to source files on disk | `false` |
| `-i, --incremental` | `-i` | Use snapshot baseline to analyze only new/modified files | `false` |
| `-u, --update-snapshot` | `-u` | Refresh the snapshot baseline | `false` |
| `-dt, --duplication-tolerance <n>` | `-dt` | Maximum new clone clusters tolerated in incremental mode | `0` |
| `--no-comments` | | Disable comment extraction and LLM summarization | `false` |
| `--enable-comments` | | Explicitly enable comment extraction and summarization | `true` |
| `--no-duplication` | | Disable structural code clone detection | `false` |
| `--enable-duplication` | | Explicitly enable structural code clone detection | `true` |
| `-ts, --threshold-statements <n>` | `-ts` | Minimum statement count to analyze a method for clones | `8` |
| `-tc, --threshold-comments <n>` | `-tc` | Minimum character length for comments to be flagged as verbose | `100` |
| `-txs, --threshold-xml-summary <n>` | `-txs` | Minimum content length for XML `<summary>` / `<remarks>` tags | `threshold-comments` (`100`) |
| `-txp, --threshold-xml-param <n>` | `-txp` | Minimum content length for XML `<param>` tags | `threshold-comments / 2` (`50`) |
| `-m, --model <name>` | `-m` | LLM model tag for comment summarization | `gemma4:e4b` |
| `-e, --endpoint <url>` | `-e` | Ollama endpoint URL | `http://localhost:11434` |
| `-cs, --context-size <n>` | `-cs` | Ollama context window size (`num_ctx`) | `24576` |
| `-c, --config <path>` | `-c` | Explicit path to `codecondenser.json` configuration file | Auto-detected |
| `--no-color` | | Disable ANSI color formatting in console output | `false` |
| `-h, --help` | | Show usage instructions and command help | |
| `-v, --version` | | Show tool version information | |

### Exit Codes

| Exit Code | Name | Meaning |
| :---: | :--- | :--- |
| `0` | **Success** | Analysis clean; no policy violations. |
| `1` | **General Error** | Invalid arguments, missing file, or unhandled exception. |
| `2` | **Duplication Violation** | In incremental mode, newly introduced code clones exceed `duplicationTolerance`. Outputs refactoring advice for developers and AI agents. |
| `130` | **Canceled** | Execution canceled by user (`Ctrl+C`). |

---

## Configuration (`codecondenser.json`)

`Pmad.CodeCondenser` automatically looks for a `codecondenser.json` configuration file in the target directory or working directory:

```json
{
  "enableCommentSummarization": true,
  "enableDuplicationDetection": true,
  "duplicationTolerance": 0,
  "duplicationExitCode": 2,
  "commentLengthThreshold": 100,
  "xmlSummaryLengthThreshold": 100,
  "xmlParamLengthThreshold": 50,
  "duplicationStatementThreshold": 8,
  "includeXmlDocumentation": true,
  "maxParallelLlmRequests": 4,
  "provider": "Ollama",
  "ollama": {
    "endpoint": "http://localhost:11434",
    "model": "gemma4:e4b",
    "contextSize": 24576
  },
  "snapshotFilePath": ".codecondenser/snapshot.json"
}
```

CLI options override settings from `codecondenser.json`.

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
- **`Pmad.CodeCondenser.Cli`**: Command-line interface with color-coded formatting and packaging as a packable .NET tool (`pmad-codecondenser`).

---

## License

MIT License. See [LICENSE](LICENSE) for details.

