# AI Agent Scenarios & Walkthroughs

This document illustrates practical, step-by-step scenarios for AI coding agents using `Pmad.CodeCondenser`.

---

## Scenario 1: Resolving a CI Exit Code 2 Failure

### Context
A PR build failed in CI with the following error:
```
[CodeCondenser] Duplication detected: 1 new clone cluster(s) introduced (exceeds tolerance of 0).
Cluster #1 (2 methods):
  1) src/MyApp/Services/UserNotifier.cs:42-58 (NotifyEmailAsync)
  2) src/MyApp/Services/UserNotifier.cs:64-80 (NotifySmsAsync)
Process exited with code 2.
```

### Agent Action Plan

#### Step 1: Run the check locally
```bash
pmad-codecondenser ./src --incremental --duplication-tolerance 0 --no-comments
```

#### Step 2: Analyze the duplicate methods
The agent inspects `UserNotifier.cs`:
```csharp
public async Task NotifyEmailAsync(User user, string message)
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    if (string.IsNullOrWhiteSpace(message)) return;
    
    var client = _clientFactory.CreateClient("Email");
    var payload = new { Recipient = user.Email, Body = message, Timestamp = DateTime.UtcNow };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    
    var response = await client.PostAsync("/send", content);
    response.EnsureSuccessStatusCode();
    _logger.LogInformation("Sent email notification to {Email}", user.Email);
}

public async Task NotifySmsAsync(User user, string message)
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    if (string.IsNullOrWhiteSpace(message)) return;
    
    var client = _clientFactory.CreateClient("Sms");
    var payload = new { Recipient = user.PhoneNumber, Body = message, Timestamp = DateTime.UtcNow };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    
    var response = await client.PostAsync("/send", content);
    response.EnsureSuccessStatusCode();
    _logger.LogInformation("Sent sms notification to {Phone}", user.PhoneNumber);
}
```

#### Step 3: Choose Resolution Strategy

##### Strategy A: Refactor into a Shared Helper (Recommended)
Extract the common HTTP dispatch logic:
```csharp
private async Task SendNotificationPayloadAsync(string clientName, string recipient, string body, string channelName)
{
    var client = _clientFactory.CreateClient(clientName);
    var payload = new { Recipient = recipient, Body = body, Timestamp = DateTime.UtcNow };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    
    var response = await client.PostAsync("/send", content);
    response.EnsureSuccessStatusCode();
    _logger.LogInformation("Sent {Channel} notification to {Recipient}", channelName, recipient);
}

public async Task NotifyEmailAsync(User user, string message)
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    if (string.IsNullOrWhiteSpace(message)) return;
    await SendNotificationPayloadAsync("Email", user.Email, message, "email");
}

public async Task NotifySmsAsync(User user, string message)
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    if (string.IsNullOrWhiteSpace(message)) return;
    await SendNotificationPayloadAsync("Sms", user.PhoneNumber, message, "sms");
}
```

##### Strategy B: Suppress if Intentional or Decoupled
If refactoring is not desirable (e.g. methods are meant to evolve independently):
```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeCondenser", "Duplication", Justification = "Distinct notification channels evolving independently")]
public async Task NotifySmsAsync(User user, string message)
{
    // ...
}
```
Or with a comment marker:
```csharp
// codecondenser:ignore-duplicate
public async Task NotifySmsAsync(User user, string message)
{
    // ...
}
```

#### Step 4: Verify
Re-run the command:
```bash
pmad-codecondenser ./src --incremental --duplication-tolerance 0 --no-comments
```
Expected output:
```
[CodeCondenser] Duplication check passed: 0 new clone clusters introduced.
Exit code: 0
```

---

## Scenario 2: Condensing Verbose Comments in a Repository

### Context
A user asks: *"Clean up the verbose comments in the project using local Ollama model gemma4:e4b."*

### Agent Action Plan

#### Step 1: Run dry-run analysis
```bash
pmad-codecondenser ./src -m gemma4:e4b
```
The agent reviews the console output:
```
[CodeCondenser] Found 14 verbose comments.
[CodeCondenser] Summarizing comments with model 'gemma4:e4b'...
[CodeCondenser] Progress: [===============>] 14/14 comments summarized.
[CodeCondenser] Snapshot cached to .codecondenser/snapshot.json.
Run with --apply to apply summarized comments to source files.
```

#### Step 2: Apply the cached summaries to source code
```bash
pmad-codecondenser ./src --apply
```
The agent inspects git diff to ensure all edits are accurate:
- Summaries were wrapped within 120 columns.
- Missing `<summary>` tags on doc comments were automatically inserted.
- Inline references (`<see cref="..." />`) were preserved.

#### Step 3: Commit the persistent store
The agent verifies git status:
```bash
git status
```
Output:
```
modified:   src/MyApp/Engine.cs
modified:   src/MyApp/Parser.cs
modified:   .codecondenser/summarized-comments.json
```
The agent commits both the modified C# source files and `.codecondenser/summarized-comments.json`.

---

## Scenario 3: Protecting Critical Architecture Documentation

### Context
The user has an extensive XML documentation comment on a public API that must remain verbose and detailed:

```csharp
/// <summary>
/// Implements a lock-free ring buffer for low-latency producer-consumer message queues.
/// The buffer uses atomic memory barriers and power-of-two mask operations to guarantee
/// wait-free single-producer single-consumer throughput exceeding 40M ops/sec.
/// Refer to Lamport's Bakery Algorithm extension for memory visibility guarantees.
/// </summary>
public sealed class FastRingBuffer<T>
{
}
```

### Agent Action
Add the `condense="no"` attribute to the `<summary>` tag:
```csharp
/// <summary condense="no">
/// Implements a lock-free ring buffer for low-latency producer-consumer message queues.
/// The buffer uses atomic memory barriers and power-of-two mask operations to guarantee
/// wait-free single-producer single-consumer throughput exceeding 40M ops/sec.
/// Refer to Lamport's Bakery Algorithm extension for memory visibility guarantees.
/// </summary>
public sealed class FastRingBuffer<T>
{
}
```
Now, whenever `pmad-codecondenser` analyzes this file, it immediately ignores the `<summary>` tag.
