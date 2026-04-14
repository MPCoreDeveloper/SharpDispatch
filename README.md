<div align="center">
  <img src="SharpDispatch.jpg" alt="SharpDispatch Logo" width="200" />
</div>

# SharpDispatch

[![NuGet Version](https://img.shields.io/nuget/v/SharpDispatch.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/SharpDispatch/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg?logo=.net)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-Ready-green.svg)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot)

> **Blazingly fast CQRS command dispatching for .NET 10**  
> Zero allocations in hot paths • Native AOT ready • Zero external dependencies

## ⚡ Why SharpDispatch?

`SharpDispatch` is a lightweight, high-performance CQRS command dispatching library built on modern .NET 10 principles:

- **🚀 Sub-microsecond dispatch latency** — Singleton handlers dispatch in ~10–15ns with zero allocations
- **🎯 Native AOT ready** — Full support for ahead-of-time compilation via `CommandDispatcherBuilder`
- **📦 Zero dependencies** — Relies only on `Microsoft.Extensions.DependencyInjection.Abstractions`
- **♻️ Zero-copy friendly** — Pass `ReadOnlySpan<T>` and `Memory<T>` through commands without allocation
- **🔒 Type-safe** — Compile-time checked handler registration with exhaustive dispatch
- **🧪 Test-friendly** — In-memory dispatcher for fast, isolated unit tests
- **🌍 Standalone** — No coupling to event sourcing, databases, or messaging — works everywhere

## 📖 Core Abstractions

```csharp
/// Marker interface for commands
public interface ICommand { }

/// Handles a specific command type
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<CommandDispatchResult> HandleAsync(TCommand command, CancellationToken ct);
}

/// Dispatches commands to registered handlers
public interface ICommandDispatcher
{
    Task<CommandDispatchResult> DispatchAsync<TCommand>(
        TCommand command, 
        CancellationToken cancellationToken = default) 
        where TCommand : ICommand;
}

/// Result of command dispatch (record struct — stack allocated)
public readonly record struct CommandDispatchResult(bool Success, string? Message)
{
    public static CommandDispatchResult Ok(string? message = null) => new(true, message);
    public static CommandDispatchResult Fail(string message) => new(false, message);
}
```

## 🚀 Quick Start

### 1. Install

```bash
dotnet add package SharpDispatch
```

### 2. Define Commands & Handlers

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
    private readonly IOrderRepository _orderRepository;

    public CreateOrderCommandHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<CommandDispatchResult> HandleAsync(
        CreateOrderCommand command, 
        CancellationToken cancellationToken)
    {
        var order = new Order { Id = command.OrderId, Amount = command.Amount };
        await _orderRepository.SaveAsync(order, cancellationToken);
        
        return CommandDispatchResult.Ok($"Order {command.OrderId} created");
    }
}
```

### 3. Register & Dispatch

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register handlers
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();

// Choose your dispatcher:
// Option A: Simple DI-based dispatcher (best for most apps)
services.AddCommandDispatcher();

// Option B: High-throughput optimized dispatcher (best for APIs)
// services.AddOptimizedCommandDispatcher(cfg =>
// {
//     cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>();
// });

var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

// Dispatch!
var result = await dispatcher.DispatchAsync(
    new CreateOrderCommand { OrderId = "ORD-001", Amount = 99.99m }
);

Console.WriteLine(result.Message);
```

## 🎯 Three Dispatcher Implementations

### ServiceProviderCommandDispatcher (Default)

Resolves handlers from DI on each call. Best for:
- Most applications
- Mixed handler lifetimes (singleton, scoped, transient)
- Simple setup

```csharp
services.AddCommandDispatcher();
```

**Overhead:** ~50–200ns per dispatch (includes DI resolution)

---

### OptimizedCommandDispatcher (Recommended for High Throughput)

Pre-builds typed delegates and uses `FrozenDictionary` for zero-allocation dispatch. Best for:
- APIs handling thousands of commands/sec
- Singleton handlers
- Latency-sensitive workloads

```csharp
services.AddOptimizedCommandDispatcher(cfg =>
{
    cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>();
    cfg.AddHandler<ShipOrderCommand, ShipOrderCommandHandler>();
});
```

**Hot path:** ~10–15ns for singleton handlers (FrozenDictionary lookup + delegate invoke)

**Constructor:** O(n handlers) — runs once at startup

---

### InMemoryCommandDispatcher (For Testing)

In-memory handler registry without DI. Best for:
- Unit tests
- Scenarios where DI is unavailable
- Fast test iteration

```csharp
var dispatcher = new InMemoryCommandDispatcher();
dispatcher.RegisterHandler(new CreateOrderCommandHandler(mockRepo));

var result = await dispatcher.DispatchAsync(command);
```

---

## 🌍 Async-First Design

All APIs are fully async with cancellation token support:

```csharp
// Respects CancellationToken
await dispatcher.DispatchAsync(command, cancellationToken);

// Safe shutdown
using var cts = new CancellationTokenSource(timeout: TimeSpan.FromSeconds(10));
await dispatcher.DispatchAsync(command, cts.Token);
```

---

## 🔒 Native AOT Ready

`CommandDispatcherBuilder` enables zero-reflection, AOT-safe registration:

```csharp
services.AddOptimizedCommandDispatcher(cfg =>
{
    // No MakeGenericType, no Activator.CreateInstance — fully verifiable!
    cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>();
    cfg.AddHandler<ShipOrderCommand, ShipOrderCommandHandler>();
});
```

Publish as Native AOT:
```bash
dotnet publish -c Release --self-contained -r win-x64 \
  -p:PublishAot=true -p:TrimMode=full
```

---

## 📊 Performance

Dispatch overhead (after handler execution):

| Scenario | Latency | Allocations |
|---|---|---|
| **OptimizedCommandDispatcher** (singleton) | ~10–15 ns | **0** |
| **ServiceProviderCommandDispatcher** | ~50–200 ns | 1–2 |
| **InMemoryCommandDispatcher** | ~20–40 ns | 0 |

*Measured on modern Intel/AMD processors. Results vary by workload.*

---

## 🧪 Testing Example

```csharp
[Fact]
public async Task CreateOrderCommand_WithValidData_ShouldSucceed()
{
    // Arrange
    var mockRepository = new Mock<IOrderRepository>();
    var handler = new CreateOrderCommandHandler(mockRepository.Object);
    var dispatcher = new InMemoryCommandDispatcher();
    dispatcher.RegisterHandler(handler);

    var command = new CreateOrderCommand { OrderId = "ORD-001", Amount = 99.99m };

    // Act
    var result = await dispatcher.DispatchAsync(command);

    // Assert
    Assert.True(result.Success);
    mockRepository.Verify(
        x => x.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
        Times.Once);
}
```

---

## 🏗️ Advanced Patterns

### Scoped Handlers

Handlers with scoped lifetime are resolved per dispatch:

```csharp
services.AddOptimizedCommandDispatcher(cfg =>
{
    cfg.AddHandler<ReportGenerationCommand, ReportGenerationHandler>(
        lifetime: ServiceLifetime.Scoped);
});
```

### Custom Handler Factories

Use the base `ICommandHandler<T>` interface for complete control:

```csharp
public class CustomOrderHandler : ICommandHandler<CreateOrderCommand>
{
    private readonly IServiceProvider _services;

    public CustomOrderHandler(IServiceProvider services) => _services = services;

    public async Task<CommandDispatchResult> HandleAsync(
        CreateOrderCommand command, 
        CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        // Custom scoping logic
        return CommandDispatchResult.Ok();
    }
}
```

### Chained Dispatchers

Wrap dispatchers for logging, metrics, or middleware:

```csharp
public class LoggingDispatcher : ICommandDispatcher
{
    private readonly ICommandDispatcher _inner;
    private readonly ILogger<LoggingDispatcher> _logger;

    public LoggingDispatcher(ICommandDispatcher inner, ILogger<LoggingDispatcher> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<CommandDispatchResult> DispatchAsync<TCommand>(
        TCommand command, 
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        _logger.LogInformation("Dispatching {CommandType}", typeof(TCommand).Name);
        
        var result = await _inner.DispatchAsync(command, cancellationToken);
        
        _logger.LogInformation("Dispatch result: {Success}", result.Success);
        return result;
    }
}

// Register
services.AddCommandDispatcher();
services.Decorate<ICommandDispatcher, LoggingDispatcher>();
```

---

## 📦 What's Inside

| Type | Purpose |
|---|---|
| `ICommand` | Marker interface for commands |
| `ICommandHandler<TCommand>` | Handler contract |
| `ICommandDispatcher` | Dispatcher abstraction |
| `CommandDispatchResult` | Stack-allocated result struct |
| `ServiceProviderCommandDispatcher` | DI-based dispatcher |
| `InMemoryCommandDispatcher` | In-memory test dispatcher |
| `OptimizedCommandDispatcher` | High-performance dispatcher |
| `CommandDispatcherBuilder` | AOT-safe fluent builder |

---

## 🤝 Integration with Other Libraries

`SharpDispatch` is transport/database agnostic:

- **ASP.NET Core**: Use in controller actions or minimal APIs
- **Hosted Services**: Dispatch from background workers
- **Message Queues**: Serialize commands from Kafka/RabbitMQ and dispatch
- **gRPC**: Forward gRPC calls to command dispatch
- **Event Sourcing**: Dispatch commands that produce events (see `SharpCoreDB.CQRS`)

---

## 📚 Example: ASP.NET Core Integration

```csharp
namespace MyApp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SharpDispatch;

[ApiController]
[Route("api/orders")]
public class OrdersController(ICommandDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
    {
        var result = await dispatcher.DispatchAsync(command, HttpContext.RequestAborted);
        
        return result.Success 
            ? Ok(result) 
            : BadRequest(result.Message);
    }
}
```

---

## ⚙️ Configuration

### Target Frameworks

- **.NET 10** (requires C# 14)

### Nullable Reference Types

Always enabled. Leverage `#nullable enable`:

```csharp
public class OrderCommand : ICommand
{
    public required string OrderId { get; init; }  // Non-null required
    public string? Notes { get; init; }            // Optional
}
```

---

## 📄 License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.

---

## 🙌 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -am 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

All code must follow the project's C# 14 standards and performance guidelines.

---

## 📮 Questions?

- Open an issue for bug reports or feature requests
- Discussions are welcome for design questions
- Check existing documentation in the repository

---

<div align="center">

**[⭐ Star us on GitHub!](https://github.com/MPCoreDeveloper/SharpDispatch)**

Made with ❤️ for .NET developers who care about performance.

</div>
