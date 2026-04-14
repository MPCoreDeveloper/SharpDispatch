<div align="center">
  <img src="SharpDispatch.jpg" alt="SharpDispatch Logo" width="150" />
</div>

# SharpDispatch

> **Blazingly fast CQRS command dispatching for .NET 10**  
> Zero allocations in hot paths • Native AOT ready • Zero external dependencies

`SharpDispatch` is a lightweight, high-performance CQRS command dispatching library for .NET 10.

## 🚀 Key Features

- **⚡ Sub-microsecond dispatch** — ~10–15ns latency, zero allocations for hot paths
- **🎯 Native AOT ready** — Full ahead-of-time compilation support
- **📦 Zero dependencies** — Only `Microsoft.Extensions.DependencyInjection.Abstractions`
- **🔒 Type-safe** — Compile-time handler registration and exhaustive dispatch
- **🧪 Test-friendly** — In-memory dispatcher for fast unit tests
- **♻️ Zero-copy ready** — Pass `ReadOnlySpan<T>` and `Memory<T>` through commands

## ✨ What's Included

| Type | Purpose |
|---|---|
| `ICommand` | Marker interface for commands |
| `ICommandHandler<T>` | Handler contract |
| `ICommandDispatcher` | Dispatcher abstraction |
| `CommandDispatchResult` | Stack-allocated result (record struct) |
| `ServiceProviderCommandDispatcher` | DI-based dispatcher (default) |
| `InMemoryCommandDispatcher` | In-memory dispatcher for tests |
| `OptimizedCommandDispatcher` | High-performance FrozenDictionary-based dispatcher |
| `CommandDispatcherBuilder` | AOT-safe fluent builder |

## 🎯 Quick Start

### 1. Define a Command & Handler

```csharp
using SharpDispatch;

// Command
public class CreateOrderCommand : ICommand
{
    public required string OrderId { get; init; }
    public required decimal Amount { get; init; }
}

// Handler
public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand>
{
    public async Task<CommandDispatchResult> HandleAsync(
        CreateOrderCommand command, 
        CancellationToken cancellationToken)
    {
        // Handle command...
        return CommandDispatchResult.Ok($"Order {command.OrderId} created");
    }
}
```

### 2. Register & Dispatch

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();
services.AddCommandDispatcher();  // or AddOptimizedCommandDispatcher()

var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

var result = await dispatcher.DispatchAsync(
    new CreateOrderCommand { OrderId = "ORD-001", Amount = 99.99m }
);
```

## 🔥 Three Dispatcher Options

### 1. ServiceProviderCommandDispatcher (Default)
Resolves handlers from DI on each call.
- **Best for:** Most applications, mixed handler lifetimes
- **Overhead:** ~50–200ns per dispatch

```csharp
services.AddCommandDispatcher();
```

### 2. OptimizedCommandDispatcher (Recommended for High Throughput)
Pre-built typed delegates + FrozenDictionary = zero-allocation dispatch.
- **Best for:** APIs, high-throughput workloads
- **Hot path:** ~10–15ns for singleton handlers

```csharp
services.AddOptimizedCommandDispatcher(cfg =>
{
    cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>();
});
```

### 3. InMemoryCommandDispatcher (For Testing)
In-memory handler registry without DI.

```csharp
var dispatcher = new InMemoryCommandDispatcher();
dispatcher.RegisterHandler(new CreateOrderCommandHandler());
```

## 🌍 Native AOT Ready

Zero reflection • Fully verifiable • Ready for PublishAot.

```csharp
services.AddOptimizedCommandDispatcher(cfg =>
{
    cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>();
});
```

No `MakeGenericType`, no `Activator.CreateInstance` — just explicit generic instantiation the AOT compiler can see.

## 🧪 Perfect for Testing

```csharp
[Fact]
public async Task ShouldCreateOrder()
{
    var dispatcher = new InMemoryCommandDispatcher();
    dispatcher.RegisterHandler(new CreateOrderCommandHandler());
    
    var result = await dispatcher.DispatchAsync(
        new CreateOrderCommand { OrderId = "ORD-001", Amount = 99.99m }
    );
    
    Assert.True(result.Success);
}
```

## 📚 Learn More

For comprehensive documentation, benchmarks, and advanced patterns, visit the [GitHub repository](https://github.com/MPCoreDeveloper/SharpDispatch).

---

<div align="center">

**Questions?** Open an issue or discussion on GitHub.

Made with ❤️ for .NET developers who care about performance.

</div>
