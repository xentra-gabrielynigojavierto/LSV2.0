# C# Code Review Guide

> C# code review guide focused on memory safety, resource management, API design, async patterns, and performance. Examples assume C# 10/11/.NET 6+.

## Table of Contents

- [Resource Management and IDisposable](#resource-management-and-idisposable)
- [Nullability and References](#nullability-and-references)
- [Async and Concurrency](#async-and-concurrency)
- [API Design and Immutability](#api-design-and-immutability)
- [Error Handling and Exceptions](#error-handling-and-exceptions)
- [Performance and Allocation](#performance-and-allocation)
- [LINQ and Collections](#linq-and-collections)
- [Type Safety and Pattern Matching](#type-safety-and-pattern-matching)
- [Tooling and Build Checks](#tooling-and-build-checks)
- [Review Checklist](#review-checklist)

---

## Resource Management and IDisposable

### Always dispose with `using`

Use `using` declarations or statements to guarantee cleanup. Never rely on the GC to release unmanaged resources.

```csharp
// ❌ Bad: manual Dispose with early returns
StreamReader Open(string path)
{
    var reader = new StreamReader(path);
    if (!reader.BaseStream.CanRead)
    {
        reader.Dispose();
        return null;
    }
    return reader; // caller must remember to dispose
}

// ✅ Good: scoped using declaration
void Process(string path)
{
    using var reader = new StreamReader(path);
    // disposed automatically at end of scope
}
```

### Implement IDisposable correctly

Follow the standard pattern. If you hold unmanaged resources or nested disposables, implement `IDisposable` properly.

```csharp
// ❌ Bad: missing Dispose, GC finalizer not suppressed
public class Connection
{
    private readonly Socket _socket = new Socket(...);
}

// ✅ Good: sealed class with simple IDisposable
public sealed class Connection : IDisposable
{
    private Socket? _socket = new Socket(...);
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _socket?.Dispose();
        _socket = null;
    }
}
```

### Prefer `IAsyncDisposable` for async resources

```csharp
// ✅ Good: async cleanup for async resources
public sealed class AsyncWorker : IAsyncDisposable
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _channel.Reader.Completion;
    }
}

// Usage
await using var worker = new AsyncWorker();
```

---

## Nullability and References

### Enable nullable reference types

Enable `#nullable enable` (or project-wide) to get compiler-enforced null safety.

```csharp
// ❌ Bad: implicit nullability, no compiler help
string GetName(User user) => user.Name; // NullReferenceException at runtime

// ✅ Good: nullable annotations make intent explicit
#nullable enable
string GetName(User? user) => user?.Name ?? "Unknown";
string GetRequiredName(User user) => user.Name; // compiler ensures non-null
```

### Avoid null checks where pattern matching is clearer

```csharp
// ❌ Bad: verbose null checks
if (result != null && result.Value > 0)
    Process(result.Value);

// ✅ Good: pattern matching
if (result is { Value: > 0 } r)
    Process(r.Value);
```

### Guard clauses for argument validation

```csharp
// ✅ Good: fail fast at entry point (.NET 6+)
public void Save(Order order)
{
    ArgumentNullException.ThrowIfNull(order);
    ArgumentException.ThrowIfNullOrEmpty(order.Id);
    // ...
}
```

---

## Async and Concurrency

### Never use `async void`

`async void` exceptions are unobservable and crash the process. Only use it for event handlers.

```csharp
// ❌ Bad: exceptions silently crash the process
async void LoadDataAsync() { await FetchAsync(); }

// ✅ Good: return Task so callers can observe exceptions
async Task LoadDataAsync() { await FetchAsync(); }

// ✅ Acceptable: event handlers only
button.Click += async (s, e) => await LoadDataAsync();
```

### Always pass CancellationToken

Propagate cancellation through the call chain. Accept `CancellationToken` as the last parameter.

```csharp
// ❌ Bad: no cancellation support
public async Task<string> FetchAsync(string url)
{
    return await _http.GetStringAsync(url);
}

// ✅ Good: propagate cancellation
public async Task<string> FetchAsync(string url, CancellationToken ct = default)
{
    return await _http.GetStringAsync(url, ct);
}
```

### Avoid `.Result` and `.Wait()`

Synchronously blocking on async work causes deadlocks in contexts with a synchronization context.

```csharp
// ❌ Bad: deadlocks in ASP.NET, WPF, WinForms
var data = GetDataAsync().Result;

// ✅ Good: await all the way up
var data = await GetDataAsync();
```

### Use `ConfigureAwait(false)` in library code

```csharp
// ✅ Good: library code avoids capturing SynchronizationContext
public async Task<int> ComputeAsync(CancellationToken ct)
{
    var raw = await FetchRawAsync(ct).ConfigureAwait(false);
    return Parse(raw);
}
```

### Protect shared state with appropriate primitives

```csharp
// ❌ Bad: data race on shared field
private int _count;
void Increment() => _count++;

// ✅ Good: Interlocked for simple counters
private int _count;
void Increment() => Interlocked.Increment(ref _count);

// ✅ Good: SemaphoreSlim for async-safe locking
private readonly SemaphoreSlim _lock = new(1, 1);
async Task UpdateAsync()
{
    await _lock.WaitAsync();
    try { /* critical section */ }
    finally { _lock.Release(); }
}
```

---

## API Design and Immutability

### Prefer records and init-only properties for immutable data

```csharp
// ❌ Bad: mutable DTO with public setters
public class OrderDto
{
    public string Id { get; set; }
    public decimal Total { get; set; }
}

// ✅ Good: immutable record
public record OrderDto(string Id, decimal Total);

// ✅ Good: non-destructive mutation with 'with'
var updated = original with { Total = 99.99m };
```

### Use interfaces and dependency injection over concrete types

```csharp
// ❌ Bad: hard dependency on concrete type
public class ReportService
{
    private readonly SqlRepository _repo = new SqlRepository();
}

// ✅ Good: depend on abstraction, inject dependency
public class ReportService
{
    private readonly IRepository _repo;
    public ReportService(IRepository repo) => _repo = repo;
}
```

### Make APIs explicit with named parameters and default values

```csharp
// ❌ Bad: boolean parameter trap
void Send(string msg, bool urgent, bool retry) { }
Send("hello", true, false); // what do these booleans mean?

// ✅ Good: named parameters or expressive types
void Send(string message, bool isUrgent = false, bool enableRetry = true) { }
Send("hello", isUrgent: true);
```

### Use `IReadOnlyList` / `IReadOnlyDictionary` in return types

```csharp
// ❌ Bad: exposes internal mutable collection
public List<User> GetUsers() => _users;

// ✅ Good: return read-only view
public IReadOnlyList<User> GetUsers() => _users.AsReadOnly();
```

---

## Error Handling and Exceptions

### Use specific exception types

```csharp
// ❌ Bad: generic Exception swallows context
throw new Exception("Something went wrong");

// ✅ Good: specific and informative
throw new InvalidOperationException(
    $"Cannot complete order '{orderId}': payment not confirmed.");
```

### Don't catch `Exception` unless re-throwing or at a boundary

```csharp
// ❌ Bad: swallows all exceptions silently
try { Process(); }
catch (Exception) { }

// ✅ Good: catch specific, log, rethrow if needed
try { Process(); }
catch (IOException ex)
{
    _logger.LogError(ex, "IO failure during processing");
    throw;
}
```

### Use `when` guards to filter without unwinding the stack

```csharp
// ✅ Good: filter without re-throwing
try { await SendAsync(); }
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
{
    await Task.Delay(retryDelay);
}
```

### Prefer `Result<T>` or `OneOf` for expected failures

```csharp
// ❌ Bad: using exceptions for control flow
User GetUser(int id)
{
    var user = _db.Find(id);
    if (user == null) throw new NotFoundException();
    return user;
}

// ✅ Good: make failure part of the signature
User? FindUser(int id) => _db.Find(id);

// Or with a Result type for richer error info
Result<User> GetUser(int id) =>
    _db.Find(id) is { } user
        ? Result.Ok(user)
        : Result.Fail($"User {id} not found");
```

---

## Performance and Allocation

### Use `Span<T>` and `Memory<T>` to avoid allocations

```csharp
// ❌ Bad: allocates a new string for every split
foreach (var part in input.Split(','))
    Process(part);

// ✅ Good: zero-allocation parsing with spans
var span = input.AsSpan();
while (span.Length > 0)
{
    int idx = span.IndexOf(',');
    var part = idx >= 0 ? span[..idx] : span;
    Process(part);
    span = idx >= 0 ? span[(idx + 1)..] : default;
}
```

### Use `StringBuilder` for string concatenation in loops

```csharp
// ❌ Bad: O(n²) allocations
string result = "";
foreach (var item in items)
    result += item.ToString();

// ✅ Good: single allocation
var sb = new StringBuilder(items.Count * 16);
foreach (var item in items)
    sb.Append(item);
string result = sb.ToString();
```

### Reserve capacity for collections

```csharp
// ❌ Bad: repeated reallocation
var list = new List<int>();
for (int i = 0; i < n; i++)
    list.Add(i);

// ✅ Good: pre-allocate
var list = new List<int>(n);
for (int i = 0; i < n; i++)
    list.Add(i);
```

### Avoid boxing value types

```csharp
// ❌ Bad: boxes every int into object
object[] values = new object[100];
for (int i = 0; i < 100; i++)
    values[i] = i; // boxing

// ✅ Good: typed generic collection
List<int> values = new List<int>(100);
for (int i = 0; i < 100; i++)
    values.Add(i); // no boxing
```

### Use `ValueTask` for hot paths that often complete synchronously

```csharp
// ❌ Bad: always allocates a Task object
public async Task<int> ReadCachedAsync(string key)
{
    if (_cache.TryGetValue(key, out var val)) return val;
    return await _db.ReadAsync(key);
}

// ✅ Good: ValueTask avoids allocation on cache hit
public ValueTask<int> ReadCachedAsync(string key)
{
    if (_cache.TryGetValue(key, out var val)) return ValueTask.FromResult(val);
    return new ValueTask<int>(FetchFromDbAsync(key));
}
```

---

## LINQ and Collections

### Avoid multiple enumeration of `IEnumerable`

```csharp
// ❌ Bad: enumerates twice, may produce different results
IEnumerable<Order> orders = GetOrders();
if (orders.Any())
    Process(orders.First());

// ✅ Good: materialize once
var orders = GetOrders().ToList();
if (orders.Count > 0)
    Process(orders[0]);
```

### Prefer `HashSet<T>` for membership tests

```csharp
// ❌ Bad: O(n) per lookup
var validIds = GetIds().ToList();
var filtered = items.Where(i => validIds.Contains(i.Id));

// ✅ Good: O(1) per lookup
var validIds = GetIds().ToHashSet();
var filtered = items.Where(i => validIds.Contains(i.Id));
```

### Use `Dictionary.TryGetValue` over double lookup

```csharp
// ❌ Bad: two dictionary lookups
if (_cache.ContainsKey(key))
    return _cache[key];

// ✅ Good: single lookup
if (_cache.TryGetValue(key, out var value))
    return value;
```

### Be cautious with LINQ in hot paths

LINQ adds delegates, enumerator allocations, and indirection. For performance-sensitive loops, prefer explicit `for`/`foreach`.

```csharp
// ❌ Bad in tight loops: LINQ overhead
var sum = numbers.Where(x => x > 0).Sum();

// ✅ Good in tight loops: direct iteration
int sum = 0;
foreach (var x in numbers)
    if (x > 0) sum += x;
```

---

## Type Safety and Pattern Matching

### Use pattern matching over type casting

```csharp
// ❌ Bad: double cast, throws on mismatch
if (shape is Circle)
    Draw((Circle)shape);

// ✅ Good: safe, single cast
if (shape is Circle c)
    c.Draw();
```

### Use exhaustive switch expressions

```csharp
// ✅ Good: compiler warns on unhandled cases
double Area(Shape shape) => shape switch
{
    Circle c    => Math.PI * c.Radius * c.Radius,
    Rectangle r => r.Width * r.Height,
    Triangle t  => 0.5 * t.Base * t.Height,
    _           => throw new NotSupportedException($"Unknown shape: {shape}")
};
```

### Prefer `enum` flags over magic booleans or integers

```csharp
// ❌ Bad: unclear combination logic
void Open(bool read, bool write, bool create) { }

// ✅ Good: composable and self-documenting
[Flags]
enum FileAccess { Read = 1, Write = 2, Create = 4 }

void Open(FileAccess access) { }
Open(FileAccess.Read | FileAccess.Write);
```

### Use generic constraints to express intent

```csharp
// ❌ Bad: no constraint, runtime failure possible
T Clone<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;

// ✅ Good: constrain to what you actually need
T Clone<T>(T source) where T : class, new()
{
    var json = JsonSerializer.Serialize(source);
    return JsonSerializer.Deserialize<T>(json)!;
}
```

---

## Tooling and Build Checks

```xml
<!-- Directory.Build.props: recommended project-wide settings -->
<PropertyGroup>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisMode>All</AnalysisMode>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

```bash
# Build with full analysis
dotnet build -warnaserror

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Roslyn analyzers (Microsoft.CodeAnalysis.NetAnalyzers already included in SDK)
# Add to csproj for extra rules:
# <PackageReference Include="StyleCop.Analyzers" Version="*" PrivateAssets="all" />
# <PackageReference Include="SonarAnalyzer.CSharp" Version="*" PrivateAssets="all" />

# Formatting
dotnet format

# Vulnerability audit
dotnet list package --vulnerable
```

---

## Review Checklist

### Resource Management
- [ ] All `IDisposable` / `IAsyncDisposable` resources are wrapped in `using`
- [ ] `IDisposable` implementations follow the standard pattern
- [ ] No resource leaks on exception paths
- [ ] `async void` is only used in event handlers

### Nullability and Safety
- [ ] Nullable reference types are enabled (`#nullable enable` or project-wide)
- [ ] Null arguments are validated at entry points
- [ ] No unchecked null-coalescing or `!` suppression without justification

### Async and Concurrency
- [ ] All async methods return `Task` or `ValueTask` (not `void`)
- [ ] `CancellationToken` is accepted and propagated
- [ ] No `.Result` / `.Wait()` blocking calls
- [ ] Shared mutable state is protected by locks, atomics, or channels
- [ ] No async work is fire-and-forget without error handling

### API and Design
- [ ] Immutable types (`record`, `init`) used for data transfer objects
- [ ] Public APIs use interfaces, not concrete types
- [ ] Return types expose `IReadOnly*` collections where possible
- [ ] No boolean parameter traps; named parameters or enums used instead
- [ ] Exception types are specific and informative

### Performance
- [ ] No unnecessary allocations in hot paths (closures, LINQ, boxing)
- [ ] Collections pre-sized where count is known
- [ ] `StringBuilder` or `Span<T>` used for string manipulation
- [ ] `IEnumerable` is not enumerated more than once

### Tooling and Tests
- [ ] Builds cleanly with `TreatWarningsAsErrors`
- [ ] Nullable annotations produce no warnings
- [ ] Analyzer warnings (Roslyn / StyleCop) are addressed
- [ ] Critical async paths are covered by unit tests with cancellation scenarios
