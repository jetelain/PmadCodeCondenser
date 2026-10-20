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
   When `pmad-codecondenser --apply` or `pmad-codecondenser apply` is executed, the SHA-256 hash of each applied comment is recorded in:
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
