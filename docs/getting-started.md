# Getting Started

This guide walks through the shortest path from *nothing* to *dispatching your
first command*. It assumes you have the **.NET 10 SDK** installed.

## 1. Install the package

```bash
dotnet add package SharpDispatch
```

SharpDispatch depends only on `Microsoft.Extensions.DependencyInjection.Abstractions`,
so it stays lightweight. If you want to build a container manually (as shown in
this guide) also reference the full DI package:

```bash
dotnet add package Microsoft.Extensions.DependencyInjection
```

> ASP.NET Core projects already include the full DI package through the shared
> framework, so no extra package is required there.

## 2. Define a command and a handler

A **command** is any immutable object that implements the empty marker interface
`ICommand`. A **handler** implements `ICommandHandler<TCommand>` for exactly one
command type.

```csharp
using SharpDispatch;

// ── Command ─────────────────────────────────────────────────────────────────
public sealed record CreateOrderCommand(string OrderId, decimal Amount) : ICommand;

// ── Handler ─────────────────────────────────────────────────────────────────
public sealed class CreateOrderCommandHandler(IOrderRepository repository)
    : ICommandHandler<CreateOrderCommand>
{
    public Task<CommandDispatchResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Amount <= 0)
        {
            return Task.FromResult(CommandDispatchResult.Fail("Amount must be positive."));
        }

        repository.Save(new Order(command.OrderId, command.Amount));
        return Task.FromResult(CommandDispatchResult.Ok($"Order {command.OrderId} created."));
    }
}
```

Notes:

- Commands can be **classes** or **structs**. Structs avoid boxing on the
  optimized dispatch path (see [Advanced Patterns](advanced-patterns.md)).
- Handlers are **async-first**: `HandleAsync` returns
  `Task<CommandDispatchResult>` and receives a `CancellationToken`.
- Return `CommandDispatchResult.Ok(...)` on success and
  `CommandDispatchResult.Fail(...)` on a handled failure.

## 3. Register and dispatch

```csharp
using Microsoft.Extensions.DependencyInjection;
using SharpDispatch;

var services = new ServiceCollection();

// (a) Register the handler. Default lifetime: Singleton.
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();

// (b) Register a dispatcher. Choose ONE:
services.AddCommandDispatcher();                          // simple, DI-per-call
// services.AddOptimizedCommandDispatcher(cfg =>          // high throughput
//     cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>());

var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

// (c) Dispatch!
var result = await dispatcher.DispatchAsync(
    new CreateOrderCommand("ORD-001", 49.99m));

Console.WriteLine(result.IsSuccess ? result.Message : $"Failed: {result.Message}");
```

## 4. ASP.NET Core (minimal API)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();
builder.Services.AddCommandDispatcher();

var app = builder.Build();

app.MapPost("/orders", async (
    CreateOrderCommand command,
    ICommandDispatcher dispatcher,
    CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(command, ct);
    return result.IsSuccess
        ? Results.Ok(result)
        : Results.BadRequest(result.Message);
});

app.Run();
```

A complete, runnable version lives in [`examples/MinimalApi`](../../examples/MinimalApi).

## 5. Next steps

- Run the ready-made examples in this repository:
  - [`examples/ConsoleQuickStart`](../../examples/ConsoleQuickStart)
  - [`examples/MinimalApi`](../../examples/MinimalApi)
  - [`examples/HighPerformance`](../../examples/HighPerformance)
- Choose the right dispatcher: [Dispatchers](dispatchers.md).
- Learn lifetime and scoping rules: [Advanced Patterns](advanced-patterns.md).
