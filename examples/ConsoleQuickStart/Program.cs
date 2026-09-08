// =============================================================================
// ConsoleQuickStart — the simplest way to use SharpDispatch.
//
//   1. Define a command and a handler.
//   2. Register both with Microsoft.Extensions.DependencyInjection.
//   3. Resolve ICommandDispatcher and dispatch.
//
// Run with:  dotnet run --project examples/ConsoleQuickStart
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using SharpDispatch;

// ---- 1. Build the service collection --------------------------------------
var services = new ServiceCollection();

// The repository the handler depends on.
services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

// Register the handler (default lifetime: Singleton).
services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();

// Register the dispatcher (ServiceProviderCommandDispatcher by default).
services.AddCommandDispatcher();

var provider = services.BuildServiceProvider();

// ---- 2. Dispatch -----------------------------------------------------------
var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

Console.WriteLine("SharpDispatch Console Quick Start");
Console.WriteLine("=================================");

var create = await dispatcher.DispatchAsync(new CreateOrderCommand("ORD-1001", 49.99m));
Console.WriteLine($"Dispatch #1 -> Success={create.IsSuccess}  {create.Message}");

var invalid = await dispatcher.DispatchAsync(new CreateOrderCommand("ORD-1002", -5m));
Console.WriteLine($"Dispatch #2 -> Success={invalid.IsSuccess}  {invalid.Message}");

var repository = provider.GetRequiredService<IOrderRepository>();
Console.WriteLine($"Repository  -> {repository.Count} order(s) stored.");

// ---- 3. Testing without a container ----------------------------------------
// InMemoryCommandDispatcher is ideal for unit tests. Delegate registration is
// supported, so you do not always need a handler class.
var testDispatcher = new InMemoryCommandDispatcher();
testDispatcher.RegisterHandler<CreateOrderCommand>(command =>
    Task.FromResult(CommandDispatchResult.Ok($"VALIDATED: {command.OrderId}")));

var testResult = await testDispatcher.DispatchAsync(new CreateOrderCommand("ORD-2000", 10m));
Console.WriteLine($"In-memory   -> {testResult.Message}");

Console.WriteLine();
Console.WriteLine("Done.");
// =============================================================================
// Domain types (kept in one file for readability).
// =============================================================================

/// <summary>Command: an immutable request to create an order.</summary>
public sealed record CreateOrderCommand(string OrderId, decimal Amount) : ICommand;

/// <summary>Aggregate/entity produced by the command handler.</summary>
public sealed record Order(string OrderId, decimal Amount);

public interface IOrderRepository
{
    int Count { get; }

    void Save(Order order);
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = [];

    public int Count => _orders.Count;

    public void Save(Order order) => _orders.Add(order);
}

/// <summary>Handler: validates the command and applies the state change.</summary>
public sealed class CreateOrderCommandHandler(IOrderRepository repository) : ICommandHandler<CreateOrderCommand>
{
    public Task<CommandDispatchResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Amount <= 0)
        {
            return Task.FromResult(
                CommandDispatchResult.Fail(
                    $"Amount must be greater than zero (received {command.Amount})."));
        }

        repository.Save(new Order(command.OrderId, command.Amount));

        return Task.FromResult(
            CommandDispatchResult.Ok($"Order '{command.OrderId}' created for {command.Amount:C}."));
    }
}
