# Dispatchers

SharpDispatch ships **three dispatcher implementations** behind the single
`ICommandDispatcher` interface, plus a **decorator helper** so you can extend
behavior without changing your application code.

## Comparison at a glance

| | `ServiceProviderCommandDispatcher` | `OptimizedCommandDispatcher` | `InMemoryCommandDispatcher` |
|---|---|---|---|
| Registration | `AddCommandDispatcher()` | `AddOptimizedCommandDispatcher(...)` | `new InMemoryCommandDispatcher()` |
| Handler resolution | DI lookup **per dispatch** | Pre-built typed delegates (`FrozenDictionary`) | Explicit registry |
| Singleton handlers | resolved per dispatch | resolved **once** at startup | n/a (instances held) |
| Scoped/transient | resolved per dispatch* | resolved per dispatch* | register per instance |
| Needs a DI container | yes | yes | no |
| Native AOT safe | yes | yes (builder overload) | yes |
| `struct` command path | boxing-free call | **boxing-free, no DI** | boxing-free call |
| Best for | most apps, mixed lifetimes | APIs, hot paths | unit tests, embedded use |
| Relative overhead | ~100s of ns (DI per call) | ~10s of ns, no DI in path | ~20–40 ns |

\* Resolved from the provider the dispatcher holds. Since the dispatchers are
singletons, a *scoped* handler registered in the root container is resolved from
the **root scope**. See [Handler lifetimes & scoping](advanced-patterns.md).

---

## 1. `ServiceProviderCommandDispatcher` — the default

Registered by `AddCommandDispatcher()`. On every dispatch it asks the DI
container for `ICommandHandler<TCommand>` and invokes it.

```csharp
services.AddCommandDispatcher();
```

Best for:

- Applications where simplicity beats micro-optimization.
- Mixed handler lifetimes (singleton, scoped, transient).
- Teams that prefer the "container is the registry" mental model.

Register handlers with `AddCommandHandler`, which supports a lifetime:

```csharp
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();            // Singleton
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>(ServiceLifetime.Transient);
```

If no handler is registered for a command, `DispatchAsync` returns a failed
`CommandDispatchResult` — it does **not** throw.

---

## 2. `OptimizedCommandDispatcher` — high throughput, AOT-safe

Registered by `AddOptimizedCommandDispatcher(...)`. It pre-builds one
**typed invoker per command** at startup. Dispatch is a
`FrozenDictionary.TryGetValue` plus a delegate invocation; for singleton
handlers the DI container is never touched on the hot path.

```csharp
services.AddOptimizedCommandDispatcher(cfg =>
{
    cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>();                       // Singleton
    cfg.AddHandler<UpdateOrderCommand, UpdateOrderCommandHandler>(ServiceLifetime.Scoped);  // resolved per dispatch
});
```

The `CommandDispatcherBuilder` callback is **compile-time visible**: every
`cfg.AddHandler<TCommand, THandler>()` instantiates `TypedCommandInvoker<TCommand>`
directly, with no `MakeGenericType` and no `Activator.CreateInstance`. That is
what makes the registration **Native AOT-safe** — see [Native AOT](native-aot.md).

### Lifetime semantics in the optimized path

| Handler lifetime | Behavior |
|---|---|
| `Singleton` | Resolved **once** during dispatcher construction and baked into the delegate. Zero container calls per dispatch. |
| `Scoped` / `Transient` | Resolved from the provider on **every** dispatch (lifetime semantics preserved, minus scope creation). |

### Struct commands

When `TCommand` is a `struct`, the JIT specializes `DispatchAsync<TCommand>` and
the typed invoker receives the value **without boxing**. See
[Struct commands](advanced-patterns.md#struct-commands--the-non-boxing-path).

### Scan-based overload (for compatibility)

```csharp
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();
services.AddOptimizedCommandDispatcher();   // scans the IServiceCollection at startup
```

This overload discovers handlers by reflecting over the service collection.
It is annotated with `RequiresDynamicCode` / `RequiresUnreferencedCode` and is
**not** recommended for trimmed/AOT builds — prefer the builder overload above.

---

## 3. `InMemoryCommandDispatcher` — no DI required

Useful in unit tests, samples, and small embedded scenarios. It stores handler
instances (or delegates) in a thread-safe dictionary.

```csharp
var dispatcher = new InMemoryCommandDispatcher();

// Register an instance handler:
dispatcher.RegisterHandler(new CreateOrderCommandHandler(repository));

// ...or register a delegate (two overloads):
dispatcher.RegisterHandler<PlaceOrderCommand>((command, ct) => Task.FromResult(CommandDispatchResult.Ok()));
dispatcher.RegisterHandler<PlaceOrderCommand>(command => Task.FromResult(CommandDispatchResult.Ok()));
```

Inspecting the registry:

```csharp
dispatcher.HandlerCount;                          // number of registered handlers
dispatcher.ContainsHandler<CreateOrderCommand>(); // true/false
```

Registering a handler for a command that already has one **replaces** it
("last registration wins").

---

## 4. Decorators — extend dispatchers with `TryDecorate`

Cross-cutting concerns (logging, metrics, validation) can be applied with the
**decorator pattern**: a class that implements `ICommandDispatcher` and forwards
to an inner dispatcher.

```csharp
public sealed class LoggingCommandDispatcher(
    ICommandDispatcher inner,
    ILogger<LoggingCommandDispatcher> logger) : ICommandDispatcher
{
    public async Task<CommandDispatchResult> DispatchAsync<TCommand>(
        TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        logger.LogInformation("Dispatching {CommandType}", typeof(TCommand).Name);
        var result = await inner.DispatchAsync(command, cancellationToken);
        logger.LogInformation("Dispatch finished. Success={Success}", result.Success);
        return result;
    }
}
```

Wire it up in one line — the decorator's `ICommandDispatcher` constructor
argument is bound to the original registration and all other constructor
arguments are resolved from DI:

```csharp
services.AddCommandDispatcher();
services.TryDecorate<ICommandDispatcher, LoggingCommandDispatcher>();
```

Behavior:

- **"Try" semantics**: if no `ICommandDispatcher` registration exists the call
  is a no-op.
- The **original lifetime is preserved** for the decorated registration.
- Decorators are resolved through `ActivatorUtilities`, so this helper is a
  **startup convenience and is not Native AOT-safe**. In AOT deployments,
  construct the decorator with an explicit factory instead:

```csharp
services.AddSingleton<ICommandDispatcher>(sp =>
    new LoggingCommandDispatcher(
        sp.GetRequiredService<ServiceProviderCommandDispatcher>(),
        sp.GetRequiredService<ILogger<LoggingCommandDispatcher>>()));
```

