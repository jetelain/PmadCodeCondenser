# Pmad.CodeCondenser

[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A static analysis developer utility and .NET global tool built on .NET 10 and Roslyn (`Microsoft.CodeAnalysis.CSharp`). It analyzes C# codebases to:

1. **Detect Structural Code Clones (Type-2 Clones)**: Detect methods sharing identical structural logic (ignoring variable, parameter, and constant renames) and provide actionable refactoring advice.
2. **Condense Verbose Comments**: Identify overly verbose comments (`//`, `/* */`, and XML `///` doc comments) and condense them into clear, concise summaries using a local LLM via Microsoft.Extensions.AI and Ollama.
3. **WYSIWYG Guarantee & Incremental Snapshots**: Cache comment summaries and file hashes so that dry-run reviews match applied code exactly without LLM drift, while incremental runs only analyze changed code.

---

## Installation

Install as a .NET Global Tool:

```bash
dotnet tool install -g Pmad.CodeCondenser
```

Or run directly from the repository:

```bash
dotnet run --project src/Pmad.CodeCondenser.Cli -- [path] [options]
```

---

## Quick Start & Usage

### 1. Dry-Run Analysis (Default)

Scan your project in dry-run mode (no files modified). Reports clone clusters and generated comment summaries:

```bash
pmad-codecondenser ./src
```

### 2. Apply Mode

Rewrite source files on disk applying the summarized comments directly from the cache:

```bash
pmad-codecondenser ./src --apply
```

### 3. Incremental Analysis

Analyze only files that were added or modified since the last snapshot baseline:

```bash
pmad-codecondenser ./src --incremental
```

### 4. Refresh Snapshot Baseline

Create or refresh the snapshot baseline:

```bash
pmad-codecondenser snapshot ./src
```

---

## Command-Line Options

| Option | Alias | Description | Default |
| :--- | :--- | :--- | :--- |
| `[path]` | | Target directory or C# file to analyze | `.` (current directory) |
| `-a, --apply` | `-a` | Write shortened comments directly to source files on disk | `false` |
| `-i, --incremental` | `-i` | Use snapshot baseline to analyze only new/modified files | `false` |
| `-u, --update-snapshot` | `-u` | Refresh the snapshot baseline | `false` |
| `-dt, --duplication-tolerance <n>` | `-dt` | Maximum new clone clusters tolerated in incremental mode | `0` |
| `--no-comments` | | Disable comment extraction and LLM summarization | `false` |
| `--no-duplication` | | Disable structural code clone detection | `false` |
| `--threshold-statements <n>` | `-ts` | Minimum statement count to analyze a method for clones | `8` |
| `--threshold-comments <n>` | `-tc` | Minimum comment character length to flag as verbose | `100` |
| `--threshold-xml-summary <n>` | `-txs` | Minimum content length for XML `<summary>` / `<remarks>` tags | `100` |
| `--threshold-xml-param <n>` | `-txp` | Minimum content length for XML `<param>` tags | `50` |
| `--model <name>` | `-m` | LLM model tag for comment summarization | `gemma4:e4b` |
| `--endpoint <url>` | `-e` | Ollama endpoint URL | `http://localhost:11434` |
| `--context-size <n>` | `-cs` | Ollama context window size (`num_ctx`) | `24576` |
| `--config <path>` | `-c` | Path to custom `codecondenser.json` configuration file | Auto-detected |
| `--no-color` | | Disable ANSI color formatting in output | `false` |
| `-h, --help` | | Show usage instructions and help | |
| `-v, --version` | | Show version information | |

### Exit Codes

| Exit Code | Meaning |
| :---: | :--- |
| `0` | Success (analysis clean, within duplication tolerance) |
| `1` | General error (invalid arguments or unhandled exception) |
| `2` | **Code Duplication Policy Violation**: In incremental mode, new duplication was introduced that exceeds `duplicationTolerance`. Outputs precise, actionable feedback for AI agents. |
| `130` | Execution canceled by user (Ctrl+C) |

---

## Configuration (`codecondenser.json`)

Pmad.CodeCondenser automatically searches for a `codecondenser.json` configuration file in the target directory or working directory:

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

Each main feature can be turned off independently in settings:
- `"enableCommentSummarization": false` (or `"enableComments": false`) disables comment extraction and LLM summarization.
- `"enableDuplicationDetection": false` (or `"enableCodeDuplication": false`) disables structural code clone detection.

CLI options override settings from `codecondenser.json`.

---

## Comment Summarization Model Benchmark Results

To select the optimal default models, a comprehensive benchmark was conducted evaluating local and cloud Ollama candidates against comments extracted from reference projects (`ComicGenCLI`, `NightCodeForge`, `HugeImages`, `Pmad.Git`), judged by **`Qwen3.6-35B-A3B:latest`** (Temperature: `0.0`, strict multi-criteria JSON scoring) with a 24k context window and internal thinking disabled:

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
> **Free Cloud Tier**: The proposed cloud models (`gpt-oss:20b-cloud`, `nemotron-3-nano:30b-cloud`, `nemotron-3-super:cloud`) are available through Ollama Cloud with an included free tier. The free tier quota typically accommodates summarizing up to **50,000 comments**. To use cloud models, create an account on [ollama.com](https://ollama.com) and log in via the Ollama CLI:
> ```bash
> ollama login
> ```

*For full benchmark methodology, CLI runner, and qualitative side-by-side case studies, see [src/Pmad.CodeCondenser.Benchmark/README.md](src/Pmad.CodeCondenser.Benchmark/README.md).*

---

## Architecture

- **`Pmad.CodeCondenser.Core`**: Domain models, AST normalization (`AstNormalizer`), clone detection (`CloneDetector`), comment extraction (`VerboseCommentExtractor`), Roslyn trivia rewriting (`VerboseCommentRewriter`), incremental diffing (`SnapshotDiffer`), and analysis engine (`CodeCondenserEngine`).
- **`Pmad.CodeCondenser.Infrastructure`**: LLM integration via `Microsoft.Extensions.AI` and `OllamaSharp` (`CommentSummarizer`), and snapshot JSON storage (`JsonSnapshotStore`).
- **`Pmad.CodeCondenser.Cli`**: Command-line interface with color-coded formatting and packaging as a packable tool (`pmad-codecondenser`).

---

## License

MIT License. See [LICENSE](LICENSE) for details.
