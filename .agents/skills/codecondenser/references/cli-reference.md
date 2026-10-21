# CodeCondenser CLI & Configuration Reference

This document provides a comprehensive reference for the `pmad-codecondenser` command-line tool and its `codecondenser.json` configuration.

---

## 1. CLI Commands & Invocation

The CLI binary is named `pmad-codecondenser`. When configured as a local tool in `.config/dotnet-tools.json`, it can also be run as `dotnet pmad-codecondenser`.

```bash
pmad-codecondenser [command] [path] [options]
```

### Commands

| Command | Description | Default Target Path |
| :--- | :--- | :---: |
| `analyze [path]` | Perform analysis in dry-run mode (default command if omitted) | `.` |
| `apply [path]` | Write summarized comments directly to source files on disk | `.` |
| `snapshot [path]` | Create or update the baseline snapshot in `.codecondenser/snapshot.json` | `.` |

---

## 2. Command-Line Options & Flags

| Flag | Alias | Type | Default | Description |
| :--- | :---: | :---: | :---: | :--- |
| `[path]` | | `string` | `.` | Target directory or `.cs` file to analyze |
| `--apply` | `-a` | `switch` | `false` | Apply summarized comments directly to source files on disk |
| `--incremental` | `-i` | `switch` | `false` | Use snapshot baseline to analyze only new or modified files |
| `--update-snapshot` | `-u` | `switch` | `false` | Update or create the snapshot baseline after analysis |
| `--duplication-tolerance <n>` | `-dt` | `int` | `0` | Max new clone clusters tolerated in incremental mode before failing |
| `--no-comments` | | `switch` | `false` | Disable comment extraction and LLM summarization |
| `--enable-comments` | | `switch` | `true` | Explicitly enable comment extraction and summarization |
| `--no-duplication` | | `switch` | `false` | Disable structural code clone detection |
| `--enable-duplication` | | `switch` | `true` | Explicitly enable structural code clone detection |
| `--threshold-statements <n>` | `-ts` | `int` | `8` | Minimum statement count for a method to be analyzed for clones |
| `--threshold-comments <n>` | `-tc` | `int` | `100` | Minimum character length for comments to be flagged as verbose |
| `--threshold-xml-summary <n>` | `-txs` | `int` | `100` | Minimum length for XML `<summary>` and `<remarks>` content |
| `--threshold-xml-param <n>` | `-txp` | `int` | `50` | Minimum length for XML `<param>` and `<typeparam>` content |
| `--max-line-length <n>` | `-mll` | `int` | `120` | Max line length for formatted XML comments |
| `--summarized-comments <path>` | `-sc` | `string` | `.codecondenser/summarized-comments.json` | Path to persistent store file stored in git |
| `--model <name>` | `-m` | `string` | `gemma4:e4b` | LLM model tag for comment summarization |
| `--endpoint <url>` | `-e` | `string` | `http://localhost:11434` | Ollama service endpoint URL |
| `--context-size <n>` | `-cs` | `int` | `24576` | Ollama context window size (`num_ctx`) |
| `--config <path>` | `-c` | `string` | Auto | Path to custom `codecondenser.json` config file |
| `--global-config <path>` | `-gc` | `string` | Auto (`~/.codecondenser/codecondenser.json`) | Path to custom global user profile configuration file |
| `--no-global-config` | | `switch` | `false` | Disable loading of global user profile configuration file |
| `--format <type>` | `-f` | `string` | `console` | Report format: `console`, `sarif`, `json` |
| `--output <path>` | `-o` | `string` | `null` | Output file path to write report to (stdout if omitted) |
| `--no-color` | | `switch` | `false` | Disable ANSI color sequences in output |
| `--help` | `-h` | `switch` | | Display help message and exit |
| `--version` | `-v` | `switch` | | Display version information and exit |

---

## 3. Exit Codes

| Code | Constant | Meaning |
| :---: | :--- | :--- |
| `0` | `Success` | Analysis completed clean; no policy violations. |
| `1` | `Error` | Fatal error, bad arguments, missing path, or unhandled exception. |
| `2` | `DuplicationDetected` | In incremental mode, newly introduced code clones exceed `--duplication-tolerance`. |
| `130` | `Canceled` | Execution canceled by user (`Ctrl+C` / `SIGINT`). |

---

## 4. Configuration Files & Hierarchy

`Pmad.CodeCondenser` supports a multi-tier configuration hierarchy:
1. **CLI Arguments** (highest precedence)
2. **Project Configuration** (`codecondenser.json` in the target directory or specified via `--config`)
3. **Global User Profile Configuration** (`~/.codecondenser/codecondenser.json` or `config.json`, or via `--global-config`)
4. **Built-in Defaults** (lowest precedence)

### Global User Profile Configuration

To configure personal LLM defaults (Ollama endpoint, default model, context size, parallelism) without placing machine-specific endpoints in source control:
- **Location**: `~/.codecondenser/codecondenser.json` or `~/.codecondenser/config.json` (on Windows `%USERPROFILE%\.codecondenser\codecondenser.json`).
- **Environment Override**: `CODECONDENSER_GLOBAL_CONFIG` or `PMAD_CODECONDENSER_GLOBAL_CONFIG`.
- **Mode Behavior**: Configuring LLM in your global profile does **not** change the default mode from `report` to `summarize`. It allows you to invoke `--summary summarize` or `-m` without specifying `--endpoint` every time.

### Example Project / Global Configuration (`codecondenser.json`)

```json
{
  "enableCommentSummarization": true,
  "enableDuplicationDetection": true,
  "duplicationTolerance": 0,
  "duplicationExitCode": 2,
  "duplicationStatementThreshold": 8,
  "commentLengthThreshold": 100,
  "xmlSummaryLengthThreshold": 100,
  "xmlRemarksLengthThreshold": 100,
  "xmlParamLengthThreshold": 50,
  "includeXmlDocumentation": true,
  "maxCommentLineLength": 120,
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

---

## 5. Recommended LLM Models for Comment Condensation

When running comment summarization, Ollama can be used with either local or cloud models:

| Model Tag | Type | Memory / VRAM | Avg Latency | Quality / Fidelity | Recommendation |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **`gemma4:e4b`** *(Default)* | Local | ~4 GB VRAM | ~198 ms median | 9.92 / 10 | Best overall local model. Fast, precise, adheres strictly to technical meaning. |
| **`gpt-oss:20b-cloud`** | Cloud | 0 GB local VRAM | ~1.8 s | 9.92 / 10 | Best cloud model. Highest accuracy (9.88/10), free tier available via `ollama login`. |
| **`nemotron-3-nano:30b-cloud`** | Cloud | 0 GB local VRAM | ~413 ms median | 9.68 / 10 | Ultra-fast cloud model. Sub-second response with 0 GB local VRAM. |
| **`jetelain/Qwen3.5-4B:latest`** | Local | ~3.5 GB VRAM | ~212 ms median | 9.90 / 10 | Excellent local alternative with `think: false` for rapid generation. |
