# Ideas to Explore

Ideas for helping condense C# code while keeping it readable. Nothing here is committed. Each item is a candidate to evaluate.

Legend: **Effort** S/M/L, **Value** low/med/high, **Det.** = deterministic (Roslyn only, no LLM).

## 1. Deterministic condensation rules

| # | Idea | Det. | Effort | Value |
|---|------|------|--------|-------|
| 1 | Guard clauses: `if (x == null) throw ...` -> `ArgumentNullException.ThrowIfNull(x)` (also `ThrowIfNullOrEmpty`) | yes | S | high |
| 2 | Primary constructors when a constructor only assigns fields | yes | M | high |
| 3 | Records / `init` properties for pure DTOs; auto-properties instead of manual backing fields | yes | M | med |
| 4 | Syntax modernization: collection expressions, `is not null`, switch expressions, expression-bodied members, `using` declarations, target-typed `new()`, `??=` | yes | M | med |
| 5 | Dead or trivial code: unused private members and usings, no-op overrides, pass-through wrappers, redundant `else` after `return`, redundant `this.`, redundant `async`/`await` | yes | M | med |
| 6 | Trivial XML docs that restate the identifier (`Gets or sets the Name.`, `The x.`): detect by similarity to the member name, suggest removal or `<inheritdoc/>` | yes | S | high |

## 2. Structural duplication (extends clone detection)

- **Near-clone parameterization**: when clones differ only by literals, types or identifiers, show the diff and propose the extracted method's parameters or generics.
- **Test pattern duplication**: suggest `[Theory]` + `[InlineData]`/`[DataRow]` for tests that differ only in inputs and expected values. Test projects likely have the best payoff.
- **Mapping blocks**: near-identical `new X { A = s.A, B = s.B }` chains -> mapper method or extension.
- **Repeated LINQ, lambda and `switch` chains**: detect near-identical query pipelines, lambdas and `switch` mappings; suggest a shared extension method or lookup table.
- **Repeated literals**: extend `RepeatedStringDetector` to numeric literals, and flag strings equal to a member name (`nameof`).

## 3. Readability guardrails

- **Net-gain score**: tokens saved weighed against complexity (nesting depth, line width, cognitive complexity). Reject suggestions that save tokens but produce dense one-liners (nested ternaries or lambdas).
- **Ranking**: tokens saved per change; "top N quick wins" summary (similar to `RepeatedStringGainEstimator`).
- **Gain thresholds**: configurable minimum gain per rule, so low-value findings are not reported.
- **Normalized measurement**: count tokens after formatting normalization so brace and blank-line style do not skew results.

## 4. Workflow and output (CLI / agents / CI)

- Extend the existing `apply` command (instead of a separate `--fix`) to also run the deterministic rules via Roslyn `SyntaxRewriter`. All applicable fixes run by default, with opt-out flags (for example `--no-guard-clauses`, `--no-trivial-docs`, `--no-comments`) and per-rule settings in the configuration file. Keep a dry-run diff and a clean-git-tree guard.
  - **Behavior change handling**: `apply` used to only write condensed comments. The first time it would run the new code rules, it prints a warning that it will now also rewrite code, and does not rewrite code until the user accepts. Two flags:
    - `--accept`: accept the new behavior for this run only (no state saved).
    - `--always-accept`: accept and remember it by writing a flag in the settings file (for example `"acceptCodeRewrite": true`), so the warning is never shown again. Teams can also commit the flag, or set it by hand, to enable the behavior for everyone.
  - Without either flag, the code rules are skipped (comments are still applied as before) and the warning explains how to enable them. This is safe for non-interactive runs (CI, agents): nothing blocks on a prompt and no code is rewritten unless explicitly accepted. Interactive terminals could additionally offer a y/N/always prompt.
- CI baseline and ratchet: fail only on regressions versus a stored baseline (total tokens, clone count).
- Suppressions with a mandatory reason and optional expiry (`// codecondenser-ignore: reason`), and a report of stale suppressions that no longer match.
- LLM refactor bundles: for non-trivial clones, emit a JSON/prompt bundle (locations, diff, constraints) for a coding agent to act on.
- Verbosity report per file/project: tokens per LOC, comment-to-code ratio, to prioritize effort.

## 5. Visual Studio / IDE analyzers for human users

### Context
The CLI and incremental system target AI agents and CI. Human developers would benefit from in-editor feedback: squiggles and lightbulb quick fixes, with no separate step.

### Recommendation
Yes, but **only for the deterministic rules (section 1)**, and as a separate package.
The expensive, whole-solution analyses (clone detection, LLM comment condensation) do not fit the analyzer model and should stay in the CLI.

### Why it fits
- Rules 1-6 are local (single syntax tree or semantic model), cheap and have an obvious fix. That is exactly what `DiagnosticAnalyzer` + `CodeFixProvider` are for.
- Analyzers run in VS, Rider (partially), `dotnet build` and CI, so human and CI feedback stay consistent.
- Many of these checks overlap with built-in IDE style rules (IDE0xxx, IDE0290 primary constructor, IDE0270, ...). Value comes mainly from rules the SDK lacks: trivial XML docs, near-clone and test-to-Theory hints, net-gain scoring.

### Why it does not fit everything
- Analyzers must be fast and run per-keystroke. Cross-file clone detection is too heavy.
- Analyzers must target `netstandard2.0` and cannot take heavy dependencies (LLM clients, large libraries). Core logic would need to be shared in a slim, analyzer-compatible form.
- Overlap with built-in rules risks duplicate or conflicting suggestions.

### Proposed architecture
```
Pmad.CodeCondenser.Rules          (netstandard2.0, pure Roslyn rule logic, no I/O)
  ├── Pmad.CodeCondenser.Analyzers   (DiagnosticAnalyzer + CodeFixProvider, NuGet analyzer package)
  └── Pmad.CodeCondenser.Core/Cli    (reuses the same rules for `apply` and reports)
```
- One implementation per rule, shared by the analyzer quick fix and the CLI `apply` command. Behavior stays identical for humans and agents.
- Diagnostic IDs (for example `PCC001`...) are configurable via `.editorconfig` (severity, thresholds), mirroring `CodeCondenserSettings` where practical.
- Ship as a NuGet analyzer package (works in VS, `dotnet build` and CI). A VSIX is not needed.
- Default severity `suggestion`/`hidden` to avoid build noise. Teams can raise it.

### Open questions
- How do we avoid duplicating IDE built-in rules? Option: ship only rules without an IDE equivalent, or offer a different message with the token-gain estimate.
- Can the CLI settings file and `.editorconfig` be unified, or should the analyzers read `.editorconfig` only?
- Should analyzers report net-gain scores (section 3), or is that too noisy for humans?
- Should an analyzer surface CLI findings (for example reading the incremental cache or a SARIF file) so humans see clone and string diagnostics without running analysis in-process? SARIF import in VS is a cheap bridge.

### Suggested path
1. Extract rule logic (section 1, items 1 and 6 first) into a shared netstandard2.0 library.
2. Add the analyzer + code-fix package with tests (`Microsoft.CodeAnalysis.Testing`).
3. Wire the same rules into CLI `apply` (all rules on by default, opt-out flags).
4. Evaluate the SARIF output of the CLI for surfacing clone and string findings in VS.

## 6. Suggested order

1. Rules 1 and 6 (guard clauses, trivial docs), plus the net-gain score (section 3).
2. Test-to-`[Theory]` suggestions.
3. Extend `apply` with the deterministic rules (shared rule library, opt-out flags).
4. Analyzer package for the same rules.
5. SARIF export for the heavy analyses.
