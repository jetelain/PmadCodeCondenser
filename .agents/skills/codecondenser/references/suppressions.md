# CodeCondenser Suppression Reference Guide

This document specifies the exact suppression syntax, detection mechanisms, and implementation details for `Pmad.CodeCondenser`.

---

## 1. Comment Verbosity Suppressions

Comment verbosity analysis flags comments exceeding length thresholds (default: 100 characters for general comments and XML `<summary>` / `<remarks>`; 50 characters for XML `<param>`).

### Matching Logic & Regular Expressions

#### In-Text Comment Markers
When examining single-line comments (`//`), multi-line comments (`/* */`), or leading trivia:
- Search is **case-insensitive** (`StringComparison.OrdinalIgnoreCase`).
- Recognized substring patterns:
  - `codecondenser:ignore`
  - `codecondenser:ignore-comment`
  - `codecondenser-ignore`
  - `nocondense`

#### XML Attribute Regex
For XML documentation comments (`///`), attributes on XML tags are matched using the following regular expression:
```regex
(condense\s*=\s*["'](no|false|never|none|off)["']|codecondenser\s*=\s*["']ignore["']|nocondense\s*=\s*["'](true|yes|1)["'])
```
Additionally, bare `nocondense` (without attribute value) is recognized when parsed via XML or plain text.

---

### Suppression Patterns & Examples

#### A. Preceding Trivia Comment
Place the ignore marker on a comment line directly preceding the target comment:
```csharp
// codecondenser:ignore
// This next comment is very long and explains why a specific legacy Windows API
// call requires custom marshaling flags and cannot use the modern runtime wrapper.
[DllImport("user32.dll")]
private static extern bool SetWindowPos(...);
```
*Note*: Intermediate whitespace and newline trivia between the ignore comment and the target comment are skipped automatically.

#### B. Inline Comment Marker
Embed any valid marker inside the comment text itself:
```csharp
// The mathematical justification for this custom PRNG is described in RFC 8628 (codecondenser:ignore)
// and uses state polynomial x^128 + x^127 + x^126 + x^121 + 1 to prevent bit collisions.
ulong seed = ComputeSeed();
```
Or in a block comment:
```csharp
/* nocondense
 * Detailed architectural overview:
 * 1. Step A initializes device context
 * 2. Step B verifies DMA buffers
 * 3. Step C registers callback with kernel driver
 */
```

#### C. XML Documentation Tags
Add suppression attributes to individual XML tags:
```csharp
/// <summary condense="no">
/// Represents the fundamental contract for distributed transaction coordinators.
/// Requires 2-phase commit support from all registered resource managers.
/// </summary>
/// <remarks condense="false">
/// Highly detailed remarks explaining transaction timeouts, lock escalation,
/// and crash recovery strategies.
/// </remarks>
/// <param name="transactionId" condense="off">Unique 128-bit UUID identifier.</param>
/// <returns>True if the transaction prepared successfully; otherwise false.</returns>
public async Task<bool> PrepareAsync(Guid transactionId)
```

Other valid attribute formats:
- `<summary nocondense="true">...</summary>`
- `<summary nocondense="yes">...</summary>`
- `<summary nocondense="1">...</summary>`
- `<summary nocondense>...</summary>`
- `<summary codecondenser="ignore">...</summary>`
- `<remarks condense="never">...</remarks>`
- `<remarks condense="none">...</remarks>`

---

## 2. Code Duplication (Clone) Suppressions

CodeCondenser extracts methods containing at least `duplicationStatementThreshold` statements (default: `8`) and normalizes their ASTs (Type-2 clone detection). Duplicate method logic is flagged unless suppressed.

### Detection Mechanism (`DuplicationIgnoreMarker`)
The engine checks:
1. Method attributes (`method.AttributeLists`)
2. Leading trivia comments directly above the method or its attributes (`method.GetLeadingTrivia()`)
3. All comment trivia within the method body (`method.Body.DescendantTrivia()`)

---

### Suppression Patterns & Examples

#### A. Standard `[SuppressMessage]` Attribute (Recommended)
Use the standard .NET code analysis attribute:
```csharp
using System.Diagnostics.CodeAnalysis;

[SuppressMessage("CodeCondenser", "Duplication", Justification = "Performance-critical specialized loop")]
public void ProcessFloatArray(float[] data)
{
    for (int i = 0; i < data.Length; i++)
    {
        // Algorithm logic identical to ProcessDoubleArray...
    }
}
```
*Matching Rule*: Any `[SuppressMessage]` attribute whose arguments contain `"codecondenser"`, `"duplicate"`, `"duplication"`, or `"clone"` (case-insensitive) suppresses the method.

#### B. Dedicated Custom Attributes
Any attribute name containing:
- `CodeCondenserIgnore`
- `NoClone`
- `IgnoreDuplication`
- `IgnoreClone`
- `SuppressDuplication`

Example:
```csharp
[CodeCondenserIgnore]
public void ProcessDoubleArray(double[] data)
{
    // ...
}
```

#### C. Leading Trivia Comment
Place an ignore marker directly before the method declaration or its attributes:
```csharp
// codecondenser:ignore-duplicate
public async Task<ResponseA> SendRequestAAsync(RequestA request, CancellationToken ct)
{
    // ...
}
```
*Supported comment markers*:
- `codecondenser:ignore-duplicate`
- `codecondenser:ignore-duplication`
- `codecondenser:ignore-clone`
- `codecondenser:ignore`
- `no-clone`
- `noclone`
- `nocondense`

#### D. Comment Marker Inside Method Body
Place the marker anywhere inside the body:
```csharp
public async Task<ResponseB> SendRequestBAsync(RequestB request, CancellationToken ct)
{
    // codecondenser:ignore-duplicate
    using var client = new HttpClient();
    var payload = JsonSerializer.Serialize(request);
    // ...
}
```

---

## 3. Persistent Comment Storage (`summarized-comments.json`)

To prevent summarized comments from being repeatedly re-analyzed (e.g. if the condensed version is 115 characters and the threshold is 100):

1. **How It Works**:
   When `codecondenser --apply` or `codecondenser apply` is executed, the SHA-256 hash of each applied comment is recorded in:
   ```
   .codecondenser/summarized-comments.json
   ```
2. **Git Tracking**:
   This JSON file must be committed to Git. When other developers or CI agents check out the repo, CodeCondenser loads this file and treats all listed hashes as ignored.
3. **Format**:
   ```json
   {
     "a1b2c3d4e5f6...": "/// <summary>Condensed summary.</summary>",
     "f6e5d4c3b2a1...": "// Shortened single line comment."
   }
   ```
4. **Local Snapshot vs Git Store**:
   - `.codecondenser/snapshot.json`: Local cache of AST hashes and current session summaries. **Add to `.gitignore`**.
   - `.codecondenser/summarized-comments.json`: Cross-session historical store. **Commit to Git**.

---

## 4. Single Type Per File Suppressions

When multiple types reside in a single `.cs` file and a secondary type must intentionally remain in the same file rather than being split out:

### A. Preceding Comment Marker
Place `// codecondenser:ignore-type-split` or `// codecondenser:ignore` on the comment line directly preceding the type declaration or its attributes:

```csharp
// codecondenser:ignore-type-split
public class AuxiliaryHelper
{
    // ...
}
```

*Supported comment markers*:
- `codecondenser:ignore-type-split`
- `codecondenser:ignore-split`
- `codecondenser:ignore-type`
- `codecondenser:ignore`
- `nocondense`

### B. Suppression Attribute
Apply `[SuppressMessage]` or a custom ignore attribute to the type declaration:

```csharp
[SuppressMessage("CodeCondenser", "SingleTypePerFile", Justification = "Tightly coupled to primary entity")]
public enum OrderStatus
{
    Draft,
    Active,
    Archived
}
```

*Supported attributes*:
- `[SuppressMessage("CodeCondenser", "SingleTypePerFile")]`
- `[SuppressMessage("CodeCondenser", "CC007")]`
- `[SingleTypePerFileIgnore]`
- `[CodeCondenserIgnore]`

### C. Automatic Tolerances
The following types are automatically exempted from splitting without needing explicit suppression:
1. **`file`-scoped types**: Any type with the C# 11 `file` modifier (e.g. `file class CacheHolder`).
2. **Line tolerance**: Types whose pure lines of code (comments and blank lines excluded) do not exceed `--type-line-tolerance` / `-tlt` (default: `1`, e.g. `public delegate void Callback(int val);`).

---

## 5. Repeated Inline String Suppressions

CodeCondenser scans for repeated inline strings exceeding length thresholds (default: 100 characters for new constants, 20 characters when matching existing `const` or `static readonly` string declarations).

### A. Inline or Preceding Comment Marker
Place `// codecondenser:ignore-string` (or `// nocondense`) on the line directly preceding or inline with the string literal:

```csharp
// codecondenser:ignore-string
string query = "SELECT id, name, email, created_at, status, last_login_time FROM users WHERE is_active = 1";
```

*Supported comment markers*:
- `codecondenser:ignore-string`
- `codecondenser:ignore-strings`
- `codecondenser:ignore`
- `nocondense`

### B. Suppression Attribute
Apply `[SuppressMessage]` to the enclosing method, property, or class:

```csharp
[SuppressMessage("CodeCondenser", "RepeatedString", Justification = "Long SQL query intentionally inlined for readability")]
public async Task<List<User>> GetActiveUsersAsync()
{
    // ...
}
```

*Supported attributes*:
- `[SuppressMessage("CodeCondenser", "RepeatedString")]`
- `[SuppressMessage("CodeCondenser", "CC006")]`
- `[CodeCondenserIgnore]`

---

## 6. Lines of Code Limits Suppressions

CodeCondenser scans for methods exceeding 150 lines of code and classes/records exceeding 1500 lines of code (pure lines of code, excluding comments and blank lines).

### A. Preceding or Inline Comment Marker
Place `// codecondenser:ignore-loc` (or `// noloc`) on the line directly preceding the method, constructor, property accessor, class, or record declaration:

```csharp
// codecondenser:ignore-loc
public class ComplexStateMachine
{
    // codecondenser:ignore-loc
    public void ProcessTransitions()
    {
        // ... large method logic ...
    }
}
```

*Supported comment markers*:
- `codecondenser:ignore-loc`
- `codecondenser:ignore-lines`
- `codecondenser:ignore-length`
- `codecondenser:ignore-method-length`
- `codecondenser:ignore-class-length`
- `codecondenser:ignore`
- `nocondense`
- `noloc`

### B. Suppression Attribute
Apply `[SuppressMessage]` to the method or class declaration:

```csharp
[SuppressMessage("CodeCondenser", "MethodLength", Justification = "Parser switch-case dispatch block")]
public void ParseTokens()
{
    // ...
}

[SuppressMessage("CodeCondenser", "ClassLength", Justification = "Large aggregate root with domain events")]
public class OrderAggregate
{
    // ...
}
```

*Supported attributes*:
- `[SuppressMessage("CodeCondenser", "MethodLength")]` (or rule `CC008`)
- `[SuppressMessage("CodeCondenser", "ClassLength")]` (or rule `CC009`)
- `[SuppressMessage("CodeCondenser", "LinesOfCode")]`
- `[SuppressMessage("CodeCondenser", "MemberLength")]`
- `[SuppressMessage("CodeCondenser", "TypeLength")]`
- `[LinesOfCodeIgnore]`
- `[CodeCondenserIgnore]`

### C. Automatic Exemptions
The following files and declarations are automatically exempted from lines of code limit checks:
1. **Generated code files**: Files matching `*.Designer.cs`, `*.g.cs`, `*.generated.cs`, `*ModelSnapshot.cs`, files in `**/Migrations/**`, or files starting with `<auto-generated>` or `<autogenerated>` comment headers.
2. **Generated code attributes**: Types or members decorated with `[GeneratedCode]` or `[CompilerGenerated]`.
3. **Excluded files & directories**: Files matching `--exclude` or `.codecondenser.json` exclude patterns.

---

## 6. Predicate / Boolean Condition Suppressions

Predicate clone analysis flags duplicate complex conditions across `if`, ternary (`? :`), `while`, `do`, boolean `return <expr>;`, and LINQ calls (e.g. `.Where(...)`, `.Any(...)`, `.All(...)`, `.First(...)`, `.Count(...)`), or conditions matching existing helper methods/properties.

### A. Comment Marker
Place `// codecondenser:ignore-predicate` (or `// nopredicate`, `// codecondenser:ignore`) on the line directly preceding the statement, or inline:

```csharp
// codecondenser:ignore-predicate
if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
{
    // ...
}
```

*Supported comment markers*:
- `codecondenser:ignore-predicate`
- `codecondenser:ignore-condition`
- `codecondenser:ignore-bool`
- `codecondenser:ignore`
- `nocondense`
- `nopredicate`

### B. Suppression Attribute
Apply `[SuppressMessage]` to the enclosing method or class declaration:

```csharp
[SuppressMessage("CodeCondenser", "PredicateDuplication", Justification = "Performance-critical inline branch")]
public void ProcessTask(int? taskId, string? stageId)
{
    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
    {
        // ...
    }
}
```

*Supported attributes*:
- `[SuppressMessage("CodeCondenser", "PredicateDuplication")]` (or rule `CC010`)
- `[SuppressMessage("CodeCondenser", "PredicateMatchesExistingHelper")]` (or rule `CC011`)
- `[SuppressMessage("CodeCondenser", "PredicateExistingHelper")]`
- `[SuppressMessage("CodeCondenser", "PredicateClone")]`
- `[PredicateIgnore]`
- `[CodeCondenserIgnore]`

---

## 7. Condensation Pattern Suppressions

Condensation pattern analysis flags verbose legacy code patterns and suggests modern C# / .NET idioms (PCC401 through PCC409).

### A. Comment Marker
Place `// codecondenser:ignore-pattern` (optionally targeting specific rule IDs like `:PCC401` or `:PCC401,PCC402`), `// codecondenser:ignore`, or `// nocondense` on the line directly preceding the target statement or expression:

```csharp
// codecondenser:ignore-pattern:PCC409
var value = a != null ? a : fallback;
```

*Supported comment markers*:
- `codecondenser:ignore-pattern` (e.g. `// codecondenser:ignore-pattern:PCC401, PCC402`)
- `codecondenser:ignore-condensation`
- `codecondenser:ignore-modernization`
- `codecondenser:ignore`
- `nocondense`

### B. Suppression Attribute
Apply `[SuppressMessage]` to the enclosing member or class declaration:

```csharp
[SuppressMessage("CodeCondenser", "PCC401", Justification = "Preserves legacy newline behavior")]
public void ProcessInput(string text)
{
    // ...
}
```

*Supported attributes*:
- `[SuppressMessage("CodeCondenser", "<ruleId>")]` (e.g. `PCC401`, `PCC402`, etc.)
- `[CodeCondenserIgnore]`
- `[IgnorePattern]`
- `[NoCondense]`
- `[SuppressCondensation]`

