// =============================================================================
// HighPerformance — the optimized dispatcher and struct commands.
//
// Demonstrates:
//   1. AOT-safe fluent registration through CommandDispatcherBuilder.
//   2. A struct command, which avoids boxing on the dispatch hot path.
//   3. A rough Stopwatch comparison between ServiceProviderCommandDispatcher
//      and OptimizedCommandDispatcher.
//
// NOTE: This is an intentionally lightweight probe. For publishable numbers use
// a proper benchmarking tool such as BenchmarkDotNet.
//
// Run with:  dotnet run --project examples/HighPerformance -c Release
// =============================================================================

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SharpDispatch;

const int Iterations = 250_000;

using var providerA = new ServiceCollection()
    .AddCommandHandler<ShipOrderCommand, ShipOrderCommandHandler>()
    .AddCommandDispatcher()
    .BuildServiceProvider();

using var providerB = new ServiceCollection()
    .AddOptimizedCommandDispatcher(cfg =>
        cfg.AddHandler<ShipOrderCommand, ShipOrderCommandHandler>())
    .BuildServiceProvider();

var dispatcherA = providerA.GetRequiredService<ICommandDispatcher>();
var dispatcherB = providerB.GetRequiredService<ICommandDispatcher>();

var command = new ShipOrderCommand("ORD-777", 3);

// Warm up both paths so caches/JIT are primed before measuring.
for (var i = 0; i < 10_000; i++)
{
    _ = await dispatcherA.DispatchAsync(command);
    _ = await dispatcherB.DispatchAsync(command);
}

var nsA = await MeasureAsync(dispatcherA, command, Iterations);
var nsB = await MeasureAsync(dispatcherB, command, Iterations);

Console.WriteLine("SharpDispatch dispatch latency probe");
Console.WriteLine("=====================================");
Console.WriteLine();
Console.WriteLine($"{"Dispatcher",-42} {"ns/op",-12} {"ops/sec",-14}");
Console.WriteLine(new string('-', 72));
Console.WriteLine(
    $"{nameof(ServiceProviderCommandDispatcher),-42} {nsA,12:N1} {ToOpsPerSecond(nsA),14:N0}");
Console.WriteLine(
    $"{nameof(OptimizedCommandDispatcher),-42} {nsB,12:N1} {ToOpsPerSecond(nsB),14:N0}");
Console.WriteLine();
Console.WriteLine($"Optimized path is ~{nsA / nsB:N1}x faster in this run.");
Console.WriteLine("(Numbers exclude handler work and vary by machine/workload.)");

static async Task<double> MeasureAsync(
    ICommandDispatcher dispatcher,
    ShipOrderCommand command,
    int iterations)
{
    var stopwatch = Stopwatch.StartNew();

    for (var i = 0; i < iterations; i++)
    {
        var result = await dispatcher.DispatchAsync(command);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

    stopwatch.Stop();
    return stopwatch.Elapsed.TotalNanoseconds / iterations;
}

static double ToOpsPerSecond(double nanosecondsPerOp)
    => nanosecondsPerOp > 0 ? 1_000_000_000d / nanosecondsPerOp : 0d;

// =============================================================================
// Domain types (kept in one file for readability).
// =============================================================================

/// <summary>
/// Struct command. Because it is a value type, the JIT-specialised dispatch
/// path in OptimizedCommandDispatcher never boxes it.
/// </summary>
public readonly record struct ShipOrderCommand(string OrderId, int Units) : ICommand;

/// <summary>Stateless handler — safe to register as a singleton.</summary>
public sealed class ShipOrderCommandHandler : ICommandHandler<ShipOrderCommand>
{
    public Task<CommandDispatchResult> HandleAsync(
        ShipOrderCommand command,
        CancellationToken cancellationToken)
        => Task.FromResult(
            CommandDispatchResult.Ok($"Shipped {command.Units} unit(s) of '{command.OrderId}'."));
}
