# Advanced Patterns

## Struct commands — the non-boxing path

`TCommand` can be any type that implements `ICommand`, including **value types**.
When a struct command flows through `OptimizedCommandDispatcher`, the JIT
specializes the generic dispatch method and hands the value straight to the
typed invoker — **no boxing, no extra allocation**.

```csharp
public readonly record struct ShipOrderCommand(string OrderId, int Units) : ICommand;

public sealed class ShipOrderCommandHandler : ICommandHandler<ShipOrderCommand>
{
    public Task<CommandDispatchResult> HandleAsync(
        ShipOrderCommand command, CancellationToken cancellationToken)
        => Task.FromResult(CommandDispatchResult.Ok(
            $"Shipped {command.Units} unit(s) of '{command.OrderId}'."));
}
```

Register exactly like any other command:

```csharp
services.AddOptimizedCommandDispatcher(cfg =>
    cfg.AddHandler<ShipOrderCommand, ShipOrderCommandHandler>());
```

See the runnable [`examples/HighPerformance`](../../examples/HighPerformance)
for a latency probe.

## Passing bulk data without copying

Handlers often need payloads (files, byte blobs, large collections). Because a
command is a normal object you can carry references to shared buffers without
copying them:

```csharp
public sealed record ImportInventoryCommand(
    string Source,
    ReadOnlyMemory<byte> Payload) : ICommand;
```

`ReadOnlyMemory<T>` is immutable, cheap to share, and safe to store on a class.
> A `Span<T>`/`ReadOnlySpan<T>` **cannot** be stored on a command — spans are
> ref structs and cannot implement interfaces or live on the heap. Use
> `Memory<T>` / `ReadOnlyMemory<T>` on commands, or split your work into
> span-friendly stages *inside* the handler.

## Handler lifetimes & scoping

SharpDispatch honors the **container lifetime** you register a handler with:

```csharp
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();                     // Singleton
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>(ServiceLifetime.Scoped); // Scoped
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>(ServiceLifetime.Transient); // Transient

// Or inside the optimized builder:
services.AddOptimizedCommandDispatcher(cfg =>
    cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>(ServiceLifetime.Scoped));
```

The lifetime semantics that matter:

- **Singleton** — one instance for the process lifetime. Prefer this for
  stateless handlers; in the optimized dispatcher the instance is captured at
  startup and the container is never called again.
- **Scoped** — one instance per scope. **Important caveat:** the dispatchers are
  registered as singletons, so a scoped handler is resolved from the provider
  the dispatcher holds. When that provider is the **root container**, the handler
  effectively behaves like a singleton-as-root-scope. Scoped dependencies of the
  handler therefore live for the process lifetime too.
- **Transient** — a fresh instance per dispatch.

### The correct way to get true per-request scoping

Never resolve a scoped handler from a singleton dispatcher if the handler (or its
dependencies) holds per-request state such as a `DbContext`.

1. Register the handler as **scoped** and dispatch from inside a DI scope, e.g.
   per HTTP request (Scrutor-style decorators and MediatR follow the same rule):
   ```csharp
   app.MapPost("/orders", async (CreateOrderCommand command, IServiceScopeFactory scopes, CancellationToken ct) =>
   {
       await using var scope = await scopes.CreateAsyncScope();
       var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
       return await dispatcher.DispatchAsync(command, ct);
   });
   ```
2. Or make the **handler** scope-aware by injecting `IServiceScopeFactory` and
   creating its own scope:
   ```csharp
   public sealed class SaveOrderHandler(IServiceScopeFactory scopes)
       : ICommandHandler<SaveOrderCommand>
   {
       public async Task<CommandDispatchResult> HandleAsync(
           SaveOrderCommand command, CancellationToken cancellationToken)
       {
           await using var scope = await scopes.CreateAsyncScope();
           var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
           // ... work with db ...
           return CommandDispatchResult.Ok();
       }
   }
   ```

## Handling failures

A dispatch returns a `CommandDispatchResult` — it is a *value*, not an
exception. Handlers should:

1. Validate input and return `CommandDispatchResult.Fail("reason")` for
   expected, business-level failures.
2. Let **unexpected** exceptions bubble up (they surface to the dispatcher
   caller), or catch and convert them with `CommandDispatchResult.FromException`:

```csharp
public async Task<CommandDispatchResult> HandleAsync(
    RegisterUserCommand command, CancellationToken cancellationToken)
{
    try
    {
        await _users.CreateAsync(command, cancellationToken);
        return CommandDispatchResult.Ok();
    }
    catch (Exception ex)
    {
        return CommandDispatchResult.FromException(ex);
    }
}
```

Read the outcome with the dedicated properties — they read better in conditions
than the positional `Success` field:

```csharp
var result = await dispatcher.DispatchAsync(command);

if (result.IsFailure) { /* ... */ }
if (result.IsSuccess) { /* ... */ }
```

The dispatchers themselves only return `Fail(...)` when **no handler is
registered**; they never swallow handler exceptions.

## Cancellation & timeouts

Every dispatch accepts a `CancellationToken`. A cancelled token is rejected by
the dispatcher **before** the handler is invoked:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var result = await dispatcher.DispatchAsync(command, cts.Token);
```

Handlers should observe the token and throw `OperationCanceledException` (or
`TaskCanceledException`) when work is cancelled; treat that as control flow, not
as an application error.

## Testing recipes

`InMemoryCommandDispatcher` keeps tests free of DI ceremony. Delegate
registration makes one-off stubs trivial:

```csharp
[Fact]
public async Task CreateOrder_WithValidAmount_Succeeds()
{
    var dispatcher = new InMemoryCommandDispatcher();

    dispatcher.RegisterHandler<CreateOrderCommand>(command =>
        Task.FromResult(CommandDispatchResult.Ok($"created {command.OrderId}")));

    var result = await dispatcher.DispatchAsync(new CreateOrderCommand("ORD-1", 10m));

    Assert.True(result.IsSuccess);
    Assert.Equal("created ORD-1", result.Message);
}
```

When you *do* want to test the full DI wiring, build a real container:

```csharp
var services = new ServiceCollection();
services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();
services.AddCommandDispatcher();

await using var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

var result = await dispatcher.DispatchAsync(new CreateOrderCommand("ORD-2", 20m));

Assert.True(result.IsSuccess);
Assert.Single(provider.GetRequiredService<IOrderRepository>().All);
```

## Chaining commands

`ICommandDispatcher` is a plain interface — compose dispatches however your
workflow requires:

```csharp
public sealed class ShipAllCommandHandler(
    ICommandDispatcher dispatcher,
    IOrderRepository orders) : ICommandHandler<ShipAllCommand>
{
    public async Task<CommandDispatchResult> HandleAsync(
        ShipAllCommand command, CancellationToken cancellationToken)
    {
        foreach (var orderId in orders.GetUnshippedIds())
        {
            var result = await dispatcher.DispatchAsync(
                new ShipOrderCommand(orderId, orderId.Length), cancellationToken);

            if (result.IsFailure)
            {
                return result;
            }
        }

        return CommandDispatchResult.Ok();
    }
}
```

Register the composed handler with the *same* dispatcher that it dispatches to —
SharpDispatch does not guard against re-entrant dispatch, so keep composition
graphs acyclic.

## Performance guidance

The dispatcher overhead is only meaningful when it is **small relative to the
handler's own work**. Rules of thumb:

- Register **stateless handlers as singletons** and use
  `AddOptimizedCommandDispatcher(cfg => ...)` for the lowest overhead.
- Prefer **struct commands** for extremely hot, tiny commands.
- Return already-completed tasks (`Task.FromResult`) from synchronous handlers
  instead of `async`/`await` when there is nothing to await — this avoids
  state-machine allocations on the hot path.
- Measure with a proper tool. [`examples/HighPerformance`](../../examples/HighPerformance)
  includes a small Stopwatch probe to get you started; for publishable numbers
  use BenchmarkDotNet.
- Do not decorate or wrap the dispatcher on the hot path unless the cross-cutting
  work is genuinely needed per dispatch.

