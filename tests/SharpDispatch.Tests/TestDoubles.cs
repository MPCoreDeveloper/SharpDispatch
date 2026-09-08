// Shared sample commands, handlers and tracking helpers used across the test suite.

namespace SharpDispatch.Tests;

/// <summary>Sample class-based command.</summary>
public sealed record CreateUserCommand(string UserName, int Age) : ICommand;

/// <summary>Sample handler for CreateUserCommand with simple validation.</summary>
public sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand>
{
    public Task<CommandDispatchResult> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
        => Task.FromResult(
            command.Age < 0
                ? CommandDispatchResult.Fail("Age cannot be negative.")
                : CommandDispatchResult.Ok($"Created user '{command.UserName}'."));
}

/// <summary>Sample struct command. Exercises the non-boxing dispatch path.</summary>
public readonly record struct PingCommand(int Sequence) : ICommand;

/// <summary>Sample handler for PingCommand.</summary>
public sealed class PingCommandHandler : ICommandHandler<PingCommand>
{
    public Task<CommandDispatchResult> HandleAsync(
        PingCommand command,
        CancellationToken cancellationToken)
        => Task.FromResult(CommandDispatchResult.Ok($"pong:{command.Sequence}"));
}

/// <summary>Thread-safe counter used to observe handler instantiation counts.</summary>
public sealed class InstanceTracker
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Track() => Interlocked.Increment(ref _count);
}

/// <summary>Handler that tracks every construction, allowing tests to verify service lifetimes.</summary>
public sealed class TrackedCreateUserHandler : ICommandHandler<CreateUserCommand>
{
    private readonly InstanceTracker _tracker;

    public TrackedCreateUserHandler(InstanceTracker tracker)
    {
        _tracker = tracker;
        _tracker.Track();
    }

    public Task<CommandDispatchResult> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
        => Task.FromResult(CommandDispatchResult.Ok($"tracked:{command.UserName}"));
}
