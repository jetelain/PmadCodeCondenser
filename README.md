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

---

## Configuration (`codecondenser.json`)

Pmad.CodeCondenser automatically searches for a `codecondenser.json` configuration file in the target directory or working directory:

```json
{
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

## Comment Summarization Model Benchmark Results

To select the optimal default model, a comprehensive benchmark was conducted evaluating local Ollama candidates against comments extracted from reference projects (`ComicGenCLI`, `NightCodeForge`, `HugeImages`, `Pmad.Git`), judged by **`jetelain/Qwen3.6-35B-A3B:latest`** (Temperature: `0.0`, strict multi-criteria JSON scoring) with a 24k context window and internal thinking disabled:

| Rank | Model | Overall Score | Avg Latency | Median Latency | Throughput | Compression | Recommendation |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| 🥇 | **`gemma4:e4b`** | **9.92 / 10** | 464 ms | **198 ms** | 205.8 tok/s | **20.9%** | 🏆 **Default / Top Quality** (near-perfect technical fidelity & conciseness) |
| 🥈 | **`jetelain/Qwen3.5-4B:latest`** | **9.90 / 10** | 365 ms | 212 ms | 214.3 tok/s | 16.4% | ⚖️ **Best Balanced** (ultra-fast latency with `think: false`) |
| 🥉 | **`qwen3.5:4b`** | **9.84 / 10** | 429 ms | 236 ms | 142.8 tok/s | 20.0% | Strong XML doc summaries |
| 4 | **`gemma4:e2b`** | **9.82 / 10** | 551 ms | **126 ms** | **309.3 tok/s** | 19.7% | Highest raw generation speed |
| 5 | **`qwen3.5:2b`** | **9.54 / 10** | **342 ms** | 176 ms | 191.9 tok/s | 13.6% | ⚡ **Fastest / Lightweight** (minimal VRAM footprint) |

*For full benchmark methodology, CLI runner, and qualitative side-by-side case studies, see [src/Pmad.CodeCondenser.Benchmark/README.md](src/Pmad.CodeCondenser.Benchmark/README.md).*

---

## Architecture

- **`Pmad.CodeCondenser.Core`**: Domain models, AST normalization (`AstNormalizer`), clone detection (`CloneDetector`), comment extraction (`VerboseCommentExtractor`), Roslyn trivia rewriting (`VerboseCommentRewriter`), incremental diffing (`SnapshotDiffer`), and analysis engine (`CodeCondenserEngine`).
- **`Pmad.CodeCondenser.Infrastructure`**: LLM integration via `Microsoft.Extensions.AI` and `OllamaSharp` (`CommentSummarizer`), and snapshot JSON storage (`JsonSnapshotStore`).
- **`Pmad.CodeCondenser.Cli`**: Command-line interface with color-coded formatting and packaging as a packable tool (`pmad-codecondenser`).

---

## License

MIT License. See [LICENSE](LICENSE) for details.
