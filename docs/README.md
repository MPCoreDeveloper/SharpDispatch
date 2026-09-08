# SharpDispatch Documentation

Welcome to the SharpDispatch documentation. SharpDispatch is a lightweight,
high-performance CQRS **command dispatching** library for .NET 10.

> **Dispatch commands. Nothing else.** No event sourcing, no sagas, no message
> bus — just blazing-fast, type-safe command dispatch with three pluggable
> dispatchers and full Native AOT support.

## Quick navigation

| Guide | What it covers |
|---|---|
| [Getting Started](getting-started.md) | Install, define commands/handlers, register & dispatch |
| [Dispatchers](dispatchers.md) | The three dispatchers — when to use which, lifetimes, decorators |
| [Advanced Patterns](advanced-patterns.md) | Struct commands, scoping, exception handling, testing, performance |
| [Native AOT](native-aot.md) | Reflection-free registration for trimmed/AOT deployments |
| [API Reference](api-reference.md) | Every public type and member at a glance |

## Where to look first

1. **Install the package** — see [Getting Started](getting-started.md).
2. **Run the examples** — compile and run them from this repository:
   - `examples/ConsoleQuickStart` — the minimal end-to-end flow.
   - `examples/MinimalApi` — SharpDispatch inside an ASP.NET Core minimal API,
     including a logging decorator.
   - `examples/HighPerformance` — the optimized dispatcher with struct commands
     and a rough latency probe.
3. **Pick a dispatcher** — the [Dispatchers](dispatchers.md) page explains the
   trade-offs in plain language.

## Public surface at a glance

| Type | Purpose |
|---|---|
| `ICommand` | Marker interface for commands |
| `ICommandHandler<TCommand>` | Handler contract |
| `ICommandDispatcher` | Dispatcher abstraction |
| `CommandDispatchResult` | Value-type result (`Success`, `Message`, `IsSuccess`, `IsFailure`) |
| `ServiceProviderCommandDispatcher` | DI-based dispatcher (default) |
| `InMemoryCommandDispatcher` | Handler registry without DI — great for tests |
| `OptimizedCommandDispatcher` | FrozenDictionary + pre-built typed delegates |
| `CommandDispatcherBuilder` | Reflection-free, AOT-safe fluent registration |
| `DispatchServiceCollectionExtensions` | `AddCommandHandler`, `AddCommandDispatcher`, `AddOptimizedCommandDispatcher`, `TryDecorate` |

## Repository layout

```
src/SharpDispatch/          The library
tests/SharpDispatch.Tests/  xUnit test suite (43 tests)
examples/ConsoleQuickStart/ Simplest possible usage
examples/MinimalApi/        ASP.NET Core integration
examples/HighPerformance/   Optimized dispatcher + struct commands
docs/                       These documents
```
