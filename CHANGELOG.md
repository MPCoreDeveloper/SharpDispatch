# Changelog

All notable changes to **SharpDispatch** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-09-08

### Added

- **`CommandDispatchResult` enhancements** (backwards compatible):
  - `IsSuccess` / `IsFailure` convenience properties.
  - `FromException(Exception)` factory for converting caught exceptions into a
    failed result.
- **`AddCommandHandler<TCommand, THandler>(ServiceLifetime)`** — new overload
  that registers handlers with an explicit lifetime (previously always singleton).
- **`InMemoryCommandDispatcher`** conveniences:
  - Delegate registration overloads:
    `RegisterHandler<TCommand>(Func<TCommand, CancellationToken, Task<CommandDispatchResult>>)`
    and `RegisterHandler<TCommand>(Func<TCommand, Task<CommandDispatchResult>>)`.
  - `HandlerCount` property and `ContainsHandler<TCommand>()` method.
- **`TryDecorate<TService, TDecorator>()`** DI extension — wraps the current
  service registration with a decorator (no-op when unregistered, preserves the
  original lifetime). Makes the documented decorator/logging pattern first-class.
- **Test suite** (`tests/SharpDispatch.Tests`, xUnit, 43 tests) covering all
  dispatchers, lifetimes, registration helpers, decoration and results.
- **Runnable examples**:
  - `examples/ConsoleQuickStart` — minimal end-to-end usage.
  - `examples/MinimalApi` — ASP.NET Core minimal API with a logging decorator.
  - `examples/HighPerformance` — optimized dispatcher with struct commands.
- **Structured documentation** under `docs/` (getting started, dispatcher guide,
  advanced patterns, Native AOT guide, API reference) and this changelog.

### Changed

- Package references updated to the latest stable versions:
  - `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.9 → **10.0.11**
  - `Microsoft.SourceLink.GitHub` 10.0.300 → **10.0.400**
- CI workflow now builds the whole solution and runs the test suite.
- README / NuGet README: corrected the decorator example (now uses
  `TryDecorate`), clarified scoped-handler semantics, added links to docs and
  examples.

### Fixed

- README "Chained Dispatchers" example referenced a non-existent
  `services.Decorate<T, T>()` API; the pattern now uses `TryDecorate`.
- Misleading "zero-copy `ReadOnlySpan<T>` in commands" claim removed
  (spans are ref structs and cannot be stored on commands).

## [1.0.1] - 2026

### Changed

- SourceLink integration (`Microsoft.SourceLink.GitHub`), embedded sources and
  deterministic build settings for step-into debugging on NuGet.org.
- Package metadata, release notes and documentation refresh.

## [1.0.0] - 2026

### Added

- Initial release of SharpDispatch:
  - `ICommand`, `ICommandHandler<TCommand>`, `ICommandDispatcher`,
    `CommandDispatchResult`.
  - `ServiceProviderCommandDispatcher`, `OptimizedCommandDispatcher`,
    `InMemoryCommandDispatcher` and the reflection-free `CommandDispatcherBuilder`.
  - `DispatchServiceCollectionExtensions` (`AddCommandHandler`,
    `AddCommandDispatcher`, `AddOptimizedCommandDispatcher`).
  - Target: .NET 10 (C# 14), Native AOT-ready.
