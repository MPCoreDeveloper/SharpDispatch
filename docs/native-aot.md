# Native AOT

SharpDispatch is designed to be **Native AOT-ready**: the library itself uses no
reflection in the code paths you ship, and its single NuGet dependency is
`Microsoft.Extensions.DependencyInjection.Abstractions`.

## What "AOT-safe" means here

Ahead-of-time compilation (and full trimming) cannot run code that constructs
generic types or reflects over assemblies at runtime. SharpDispatch therefore
offers **two registration styles**:

| Registration style | Reflection at startup | AOT / trimming |
|---|---|---|
| `AddOptimizedCommandDispatcher(cfg => cfg.AddHandler<T, H>())` | none | ✅ safe |
| `AddOptimizedCommandDispatcher()` (scan-based) | `MakeGenericType` + `Activator.CreateInstance` over the service collection | ❌ annotated `RequiresDynamicCode` / `RequiresUnreferencedCode` |
| `AddCommandDispatcher()` (`ServiceProviderCommandDispatcher`) | none | ✅ safe |
| `TryDecorate<TService, TDecorator>()` | `ActivatorUtilities` constructor resolution | ⚠️ startup only — not AOT-safe |

For AOT deployments use the **builder overload**:

```csharp
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOptimizedCommandDispatcher(cfg =>
{
    cfg.AddHandler<CreateOrderCommand, CreateOrderCommandHandler>();
    cfg.AddHandler<ShipOrderCommand,   ShipOrderCommandHandler>();
});

var app = builder.Build();
// ...map endpoints...
app.Run();
```

Every `cfg.AddHandler<TCommand, THandler>()` call is an explicit generic
instantiation that the AOT compiler can see and preserve — no dynamic code
generation anywhere.

## What you get for free

- No `MakeGenericType`, no `Activator.CreateInstance`, no compiled expressions.
- Singleton handlers are resolved **once** at startup and captured in a typed
  delegate; the container is not consulted on the hot path.
- Struct commands flow through JIT-specialized invokers without boxing.

## Publishing

```bash
# Console / worker
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true

# ASP.NET Core
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true
```

If your project is set to `IsAotCompatible` (or `PublishTrimmed`), the compiler
warns you when you call an API that needs dynamic code. Use those warnings to
find accidental scan-based registrations. A clean publish shows
`Trimmed ...` and no IL2xxx/IL3xxx warnings originating from SharpDispatch
types.

## Things to keep in mind

1. **Handlers must be statically reachable.** Because registration is explicit
   in the builder, the AOT compiler sees every `THandler` — but only when the
   handler types are used from code the trimmer can follow. Registering handler
   *types* through reflection or configuration files defeats that guarantee.
2. **Do not use the scan-based overload** (`AddOptimizedCommandDispatcher()`)
   in trimmed/AOT builds — it is annotated with `[RequiresDynamicCode]` and
   `[RequiresUnreferencedCode]`.
3. **`TryDecorate` is a convenience for reflection-based hosts.** If you need a
   decorator in an AOT build, register it with an explicit factory:
   ```csharp
   services.AddSingleton<ICommandDispatcher>(sp =>
       new LoggingCommandDispatcher(
           sp.GetRequiredService<ServiceProviderCommandDispatcher>(),
           sp.GetRequiredService<ILogger<LoggingCommandDispatcher>>()));
   ```
4. The **library** is verified compatible: it ships no trimming warnings and
   `CommandDispatcherBuilder` exists precisely so consumers can register
   handlers without reflection.

## Verifying AOT compatibility

Run a publish and confirm no warnings reference SharpDispatch types:

```bash
dotnet publish -c Release -r win-x64 -p:PublishAot=true \
  | Select-String -Pattern 'IL2|IL3|SharpDispatch' -CaseSensitive:$false
```
