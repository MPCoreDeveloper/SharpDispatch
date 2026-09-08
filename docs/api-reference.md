# API Reference

All types live in the `SharpDispatch` namespace. Only two NuGet packages are
involved: the library itself and (for container-based scenarios)
`Microsoft.Extensions.DependencyInjection`.

## Abstractions

### `ICommand`

```csharp
public interface ICommand { }
```

Empty **marker interface**. Implement it on a class or struct to make it a
dispatchable command. Commands should be immutable value-like objects.

### `ICommandHandler<in TCommand>` where `TCommand : ICommand`

```csharp
public interface ICommandHandler<in TCommand>
{
    Task<CommandDispatchResult> HandleAsync(
        TCommand command, CancellationToken cancellationToken = default);
}
```

Handles exactly one command type. Implementations are registered in DI or added
to `InMemoryCommandDispatcher`.

### `ICommandDispatcher`

```csharp
public interface ICommandDispatcher
{
    Task<CommandDispatchResult> DispatchAsync<TCommand>(
        TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand;
}
```

Dispatches a command to its registered handler. Returns a
`CommandDispatchResult`; throws only for `ArgumentNullException`,
`OperationCanceledException`, or unhandled handler exceptions.

### `CommandDispatchResult` — `readonly record struct`

```csharp
public readonly record struct CommandDispatchResult(bool Success, string? Message);
```

| Member | Kind | Description |
|---|---|---|
| `Success` | positional property | `true` when handling succeeded |
| `Message` | positional property | optional result text |
| `IsSuccess` | property | `true` when handling succeeded (alias of `Success`) |
| `IsFailure` | property | `true` when handling failed (`!Success`) |
| `Ok(string? message = null)` | static factory | successful result |
| `Fail(string message)` | static factory | failed result |
| `FromException(Exception exception)` | static factory | failed result from an exception message |
| `==` / `!=` / `Equals` | record struct | value equality |

## Dispatchers

### `ServiceProviderCommandDispatcher : ICommandDispatcher`

```csharp
public sealed class ServiceProviderCommandDispatcher(IServiceProvider serviceProvider);
```

Resolves `ICommandHandler<TCommand>` from DI on every dispatch. Registered by
`AddCommandDispatcher()`. Returns a failed result when no handler is registered.

### `OptimizedCommandDispatcher : ICommandDispatcher`

FrozenDictionary lookup + pre-built typed invoker per command. Constructed only
by the DI extensions; constructors are `internal`.

- Builder (AOT-safe): `AddOptimizedCommandDispatcher(Action<CommandDispatcherBuilder>)`
- Scan-based: `AddOptimizedCommandDispatcher()` — `RequiresDynamicCode`

### `InMemoryCommandDispatcher : ICommandDispatcher`

```csharp
public sealed class InMemoryCommandDispatcher
{
    public void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler);
    public void RegisterHandler<TCommand>(Func<TCommand, CancellationToken, Task<CommandDispatchResult>> handler);
    public void RegisterHandler<TCommand>(Func<TCommand, Task<CommandDispatchResult>> handler);
    public int HandlerCount { get; }
    public bool ContainsHandler<TCommand>();
    public Task<CommandDispatchResult> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default);
}
```

Thread-safe registry for tests and DI-free scenarios. "Last registration wins".

### `CommandDispatcherBuilder`

```csharp
public sealed class CommandDispatcherBuilder
{
    public CommandDispatcherBuilder AddHandler<TCommand, THandler>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton);
}
```

Fluent, reflection-free registration used by the AOT-safe
`AddOptimizedCommandDispatcher` overload. Registering the same command twice —
the last call wins.

## Dependency injection extensions — `DispatchServiceCollectionExtensions`

```csharp
public static class DispatchServiceCollectionExtensions
{
    // Handlers
    public static IServiceCollection AddCommandHandler<TCommand, THandler>(this IServiceCollection services); // Singleton
    public static IServiceCollection AddCommandHandler<TCommand, THandler>(this IServiceCollection services, ServiceLifetime lifetime);

    // Dispatchers
    public static IServiceCollection AddCommandDispatcher(this IServiceCollection services);
    public static IServiceCollection AddOptimizedCommandDispatcher(this IServiceCollection services, Action<CommandDispatcherBuilder> configure);
    public static IServiceCollection AddOptimizedCommandDispatcher(this IServiceCollection services); // scan-based

    // Decorators
    public static IServiceCollection TryDecorate<TService, TDecorator>(this IServiceCollection services);
}
```

| Extension | Behavior |
|---|---|
| `AddCommandHandler<T, H>()` | Registers the handler as `ICommandHandler<TCommand>` (singleton). |
| `AddCommandHandler<T, H>(lifetime)` | Same, with explicit lifetime. |
| `AddCommandDispatcher()` | `TryAdd` the `ServiceProviderCommandDispatcher` as singleton `ICommandDispatcher`. |
| `AddOptimizedCommandDispatcher(cfg)` | Replaces `ICommandDispatcher` with the optimized dispatcher built from `cfg`. |
| `AddOptimizedCommandDispatcher()` | Same, but scans existing `ICommandHandler<>` registrations. |
| `TryDecorate<TService, TDecorator>()` | Wraps the current `TService` registration with `TDecorator` (no-op if absent). |

`TryDecorate` constraints: `TService : class`, `TDecorator : class, TService`.
The decorator constructor receives the resolved original `TService` plus any
other constructor arguments from DI. Annotated for trimming/AOT; startup use
only.
