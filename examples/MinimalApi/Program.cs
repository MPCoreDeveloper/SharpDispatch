// =============================================================================
// MinimalApi — SharpDispatch wired into an ASP.NET Core minimal API.
//
//   POST /orders/{orderId?}  -> dispatches CreateOrderCommand (writes via CQRS)
//   GET  /orders/{orderId}   -> reads directly from the repository (CQRS reads)
//
// The default dispatcher is decorated with LoggingCommandDispatcher using the
// TryDecorate helper shipped with SharpDispatch.
//
// Run with:  dotnet run --project examples/MinimalApi
// Try with:
//   curl -X POST http://localhost:5000/orders
//        -H "Content-Type: application/json"
//        -d '{"orderId":"ORD-1","amount":10.5}'
// =============================================================================

using SharpDispatch;

var builder = WebApplication.CreateBuilder(args);

// Register the repository and the command handler.
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddCommandHandler<CreateOrderCommand, CreateOrderCommandHandler>();

// Register the default dispatcher, then wrap it with the logging decorator.
builder.Services.AddCommandDispatcher();
builder.Services.TryDecorate<ICommandDispatcher, LoggingCommandDispatcher>();

var app = builder.Build();

// POST /orders — writes go through the command dispatcher.
app.MapPost("/orders", async (
    CreateOrderCommand command,
    ICommandDispatcher dispatcher,
    CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(command, ct);

    return result.IsSuccess
        ? Results.Created($"/orders/{command.OrderId}", result)
        : Results.BadRequest(result.Message);
});

// GET /orders/{orderId} — reads query the repository directly (CQRS read side).
app.MapGet("/orders/{orderId}", (string orderId, IOrderRepository repository) =>
{
    var order = repository.Get(orderId);
    return order is null
        ? Results.NotFound()
        : Results.Ok(order);
});

app.Run();
// =============================================================================
// Domain types (kept in one file for readability).
// =============================================================================

/// <summary>Command: an immutable request to create an order.</summary>
public sealed record CreateOrderCommand(string OrderId, decimal Amount) : ICommand;

/// <summary>Read model returned by the read side.</summary>
public sealed record Order(string OrderId, decimal Amount);

public interface IOrderRepository
{
    Order? Get(string orderId);

    void Save(Order order);
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<string, Order> _orders = new(StringComparer.Ordinal);

    public Order? Get(string orderId)
        => _orders.TryGetValue(orderId, out var order) ? order : null;

    public void Save(Order order) => _orders[order.OrderId] = order;
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
                CommandDispatchResult.Fail("Amount must be greater than zero."));
        }

        repository.Save(new Order(command.OrderId, command.Amount));

        return Task.FromResult(
            CommandDispatchResult.Ok($"Order '{command.OrderId}' created."));
    }
}

/// <summary>Decorator that logs every dispatch. Constructed by TryDecorate.</summary>
public sealed class LoggingCommandDispatcher(
    ICommandDispatcher inner,
    ILogger<LoggingCommandDispatcher> logger) : ICommandDispatcher
{
    public async Task<CommandDispatchResult> DispatchAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        logger.LogInformation(
            "Dispatching {CommandType} with {Command}",
            typeof(TCommand).Name,
            command);

        var result = await inner.DispatchAsync(command, cancellationToken);

        logger.LogInformation(
            "Dispatch {CommandType} completed. Success={Success}",
            typeof(TCommand).Name,
            result.Success);

        return result;
    }
}
