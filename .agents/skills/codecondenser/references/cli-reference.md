# CodeCondenser CLI & Configuration Reference

This document provides a comprehensive reference for the `codecondenser` command-line tool and its `codecondenser.json` configuration.

---

## 1. CLI Commands & Invocation

The CLI binary is named `codecondenser`. When configured as a local tool in `.config/dotnet-tools.json`, it can also be run as `dotnet codecondenser`.

```bash
codecondenser [command] [path] [options]
```

### Commands

| Command | Description | Default Target Path |
| :--- | :--- | :---: |
| `analyze [path]` | Perform analysis in dry-run mode (default command if omitted) | `.` |
| `apply [path]` | Write summarized comments directly to source files on disk | `.` |
| `snapshot [path]` | Create or update the baseline snapshot in `.codecondenser/snapshot.json` | `.` |
| `skills [path]` | Install or update bundled agent skills in the workspace (`.agents/skills`) or globally (`--global`) | `.` |
| `measure [path]` | Measure code in terms of estimated LLM tokens (total, comments, code) | `.` |

---

## 2. Command-Line Options & Flags

| Flag | Alias | Type | Default | Description |
| :--- | :---: | :---: | :---: | :--- |
| `[path]` | | `string` | `.` | Target directory or `.cs` file to analyze |
| `--apply` | `-a` | `switch` | `false` | Apply summarized comments directly to source files on disk |
| `--incremental` | `-i` | `switch` | `false` | Use snapshot baseline to analyze only new or modified files |
| `--update-snapshot` | `-u` | `switch` | `false` | Update or create the snapshot baseline after analysis |
| `--duplication-tolerance <n>` | `-dt` | `int` | `0` | Max new clone clusters tolerated in incremental mode before failing |
| `--string-tolerance <n>` | `-st` | `int` | `null` | Max new repeated string groups tolerated in incremental mode |
| `--type-tolerance <n>` | `-tt` | `int` | `null` | Max new type file issues tolerated in incremental mode |
| `--type-line-tolerance <n>` | `-tlt` | `int` | `1` | Max pure lines of code (comments excluded) tolerated inline |
| `--no-type-split` | | `switch` | `false` | Disable single type per file splitting |
| `--enable-type-split` | | `switch` | `true` | Explicitly enable single type per file splitting |
| `--no-type-rename` | | `switch` | `false` | Disable renaming files to match primary type name |
| `--enable-type-rename` | | `switch` | `true` | Explicitly enable renaming files to match primary type name |
| `--no-loc` | | `switch` | `false` | Disable lines of code limits detection |
| `--enable-loc` | | `switch` | `true` | Explicitly enable lines of code limits detection |
| `--threshold-method-lines <n>` | `-tml` | `int` | `150` | Maximum lines of code threshold for methods |
| `--threshold-class-lines <n>` | `-tcl` | `int` | `1500` | Maximum lines of code threshold for classes and records |
| `--loc-tolerance <n>` | `-lt` | `int` | `null` | Max new lines of code violations tolerated in incremental mode |
| `--no-comments` | | `switch` | `false` | Disable comment extraction and LLM summarization |
| `--enable-comments` | | `switch` | `true` | Explicitly enable comment extraction and summarization |
| `--no-duplication` | | `switch` | `false` | Disable structural code clone detection |
| `--enable-duplication` | | `switch` | `true` | Explicitly enable structural code clone detection |
| `--no-strings` | | `switch` | `false` | Disable repeated inline string detection |
| `--enable-strings` | | `switch` | `true` | Explicitly enable repeated inline string detection |
| `--no-predicates` | | `switch` | `false` | Disable boolean condition / predicate clone detection |
| `--enable-predicates` | | `switch` | `true` | Explicitly enable boolean condition / predicate clone detection |
| `--threshold-predicates <n>` | `-tpred` | `int` | `25` | Minimum character length for predicate clone detection |
| `--threshold-existing-helpers <n>` | `-thelp` | `int` | `20` | Minimum character length when matching against existing predicate helpers |
| `--predicate-occurrences <n>` | `-po` | `int` | `2` | Minimum occurrences for predicate clone detection |
| `--predicate-tolerance <n>` | `-pt` | `int` | `null` | Maximum new predicate clone clusters tolerated in incremental mode |
| `--threshold-statements <n>` | `-ts` | `int` | `8` | Minimum statement count for a method to be analyzed for clones |
| `--threshold-strings <n>` | `-tstr` | `int` | `100` | Minimum character length for repeated string detection |
| `--threshold-existing-constants <n>` | `-tec` | `int` | `20` | Minimum character length when matching against existing constants |
| `--string-occurrences <n>` | `-so` | `int` | `2` | Minimum occurrences for repeated string detection |
| `--threshold-comments <n>` | `-tc` | `int` | `100` | Minimum character length for comments to be flagged as verbose |
| `--threshold-xml-summary <n>` | `-txs` | `int` | `100` | Minimum length for XML `<summary>` and `<remarks>` content |
| `--threshold-xml-param <n>` | `-txp` | `int` | `50` | Minimum length for XML `<param>` and `<typeparam>` content |
| `--max-line-length <n>` | `-mll` | `int` | `120` | Max line length for formatted XML comments |
| `--summarized-comments <path>` | `-sc` | `string` | `.codecondenser/summarized-comments.json` | Path to persistent store file stored in git |
| `--model <name>` | `-m` | `string` | `gemma4:e4b` | LLM model tag for comment summarization |
| `--estimator <name>` | `-te` | `string` | `default` | Token estimator preset (`default`, `qwen`, `gemma`) for code measurement |
| `--endpoint <url>` | `-e` | `string` | `http://localhost:11434` | Ollama service endpoint URL |
| `--context-size <n>` | `-cs` | `int` | `24576` | Ollama context window size (`num_ctx`) |
| `--config <path>` | `-c` | `string` | Auto | Path to custom `codecondenser.json` config file |
| `--global-config <path>` | `-gc` | `string` | Auto (`%APPDATA%\CodeCondenser\codecondenser.json` on Windows, `~/.codecondenser/codecondenser.json` on Linux/macOS) | Path to custom global user profile configuration file |
| `--no-global-config` | | `switch` | `false` | Disable loading of global user profile configuration file |
| `--dry-run` | `-n` | `switch` | `false` | Preview actions without writing changes to disk |
| `--global` | `-g` | `switch` | `false` | Install skills globally into `~/.gemini/config/skills/` and `~/.agents/skills/` |
| `--force` | | `switch` | `false` | Force overwrite skill files even if up to date |
| `--clean` | | `switch` | `false` | Remove obsolete files from skill directories |
| `--skill <name>` | | `string` | `null` | Specify a single skill to install/update (default: all) |
| `--skills-dir <path>` | `-d`, `--dir` | `string` | `null` | Override target skills directory (default: `.agents/skills`) |
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
| `2` | `DuplicationDetected` | In incremental mode, newly introduced code clones, repeated strings, type violations, lines of code violations, or predicate clones exceed their configured tolerance. |
| `130` | `Canceled` | Execution canceled by user (`Ctrl+C` / `SIGINT`). |

---

## 4. Configuration Files & Hierarchy

`Pmad.CodeCondenser` supports a multi-tier configuration hierarchy:
1. **CLI Arguments** (highest precedence)
2. **Project Configuration** (`codecondenser.json` in the target directory or specified via `--config`)
3. **Global User Profile Configuration** (`%APPDATA%\CodeCondenser\codecondenser.json` on Windows, `~/.codecondenser/codecondenser.json` on Linux/macOS, or via `--global-config`)
4. **Built-in Defaults** (lowest precedence)

### Global User Profile Configuration

To configure personal LLM defaults (Ollama endpoint, default model, context size, parallelism) without placing machine-specific endpoints in source control:
- **Location**:
  - **Windows**: `%APPDATA%\CodeCondenser\codecondenser.json` or `config.json` (fallback: `%USERPROFILE%\.codecondenser\codecondenser.json`).
  - **Linux / macOS**: `~/.codecondenser/codecondenser.json` or `config.json` (fallback: `~/.codecondenser.json`).
- **Environment Override**: `CODECONDENSER_GLOBAL_CONFIG` or `PMAD_CODECONDENSER_GLOBAL_CONFIG` (or directory override via `CODECONDENSER_APPDATA` / `CODECONDENSER_USER_PROFILE`).
- **Mode Behavior**: Configuring LLM in your global profile does **not** change the default mode from `report` to `summarize`. It allows you to invoke `--summary summarize` or `-m` without specifying `--endpoint` every time.

### Example Project / Global Configuration (`codecondenser.json`)

```json
{
  "enableCommentSummarization": true,
  "enableDuplicationDetection": true,
  "enableRepeatedStringDetection": true,
  "enableTypeSplitting": true,
  "enableTypeRenaming": true,
  "typeLineTolerance": 1,
  "typeTolerance": null,
  "typeExitCode": 2,
  "duplicationTolerance": 0,
  "duplicationExitCode": 2,
  "repeatedStringTolerance": null,
  "repeatedStringExitCode": 2,
  "repeatedStringLengthThreshold": 100,
  "existingConstantStringLengthThreshold": 20,
  "repeatedStringMinOccurrences": 2,
  "duplicationStatementThreshold": 8,
  "commentLengthThreshold": 100,
  "xmlSummaryLengthThreshold": 100,
  "xmlRemarksLengthThreshold": 100,
  "xmlParamLengthThreshold": 50,
  "includeXmlDocumentation": true,
  "maxCommentLineLength": 120,
  "maxParallelLlmRequests": 4,
  "tokenEstimator": "default",
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
