using Xunit;

namespace SharpDispatch.Tests;

public class InMemoryCommandDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WithRegisteredInstanceHandler_Succeeds()
    {
        var dispatcher = new InMemoryCommandDispatcher();
        dispatcher.RegisterHandler(new CreateUserCommandHandler());

        var result = await dispatcher.DispatchAsync(new CreateUserCommand("alice", 30));

        Assert.True(result.Success);
        Assert.Contains("alice", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_WithCancellationAwareDelegateHandler_Works()
    {
        var dispatcher = new InMemoryCommandDispatcher();
        dispatcher.RegisterHandler<PingCommand>((command, cancellationToken) =>
            Task.FromResult(
                cancellationToken.IsCancellationRequested
                    ? CommandDispatchResult.Fail("canceled")
                    : CommandDispatchResult.Ok($"delegate:{command.Sequence}")));

        var result = await dispatcher.DispatchAsync(new PingCommand(7));

        Assert.True(result.Success);
        Assert.Equal("delegate:7", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_WithSimpleDelegateHandler_Works()
    {
        var dispatcher = new InMemoryCommandDispatcher();
        dispatcher.RegisterHandler<PingCommand>(command =>
            Task.FromResult(CommandDispatchResult.Ok($"delegate:{command.Sequence}")));

        var result = await dispatcher.DispatchAsync(new PingCommand(3));

        Assert.Equal("delegate:3", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_NoHandler_ReturnsFailure()
    {
        var dispatcher = new InMemoryCommandDispatcher();

        var result = await dispatcher.DispatchAsync(new CreateUserCommand("bob", 1));

        Assert.False(result.Success);
        Assert.Equal("No handler registered for command 'CreateUserCommand'.", result.Message);
    }

    [Fact]
    public async Task RegisterHandler_LatestRegistrationWins()
    {
        var dispatcher = new InMemoryCommandDispatcher();
        dispatcher.RegisterHandler<PingCommand>(_ => Task.FromResult(CommandDispatchResult.Ok("first")));
        dispatcher.RegisterHandler<PingCommand>(_ => Task.FromResult(CommandDispatchResult.Ok("second")));

        var result = await dispatcher.DispatchAsync(new PingCommand(1));

        Assert.Equal("second", result.Message);
        Assert.Equal(1, dispatcher.HandlerCount);
    }

    [Fact]
    public void HandlerCountAndContainsHandler_ReflectRegistrations()
    {
        var dispatcher = new InMemoryCommandDispatcher();

        Assert.Equal(0, dispatcher.HandlerCount);
        Assert.False(dispatcher.ContainsHandler<PingCommand>());

        dispatcher.RegisterHandler(new PingCommandHandler());

        Assert.Equal(1, dispatcher.HandlerCount);
        Assert.True(dispatcher.ContainsHandler<PingCommand>());
    }

    [Fact]
    public void DispatchAsync_NullCommand_ThrowsArgumentNullException()
    {
        var dispatcher = new InMemoryCommandDispatcher();
        dispatcher.RegisterHandler(new CreateUserCommandHandler());

        void Act() => dispatcher.DispatchAsync<CreateUserCommand>(null!);
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Fact]
    public async Task DispatchAsync_CancelledToken_ThrowsBeforeHandlerInvocation()
    {
        var dispatcher = new InMemoryCommandDispatcher();
        var invoked = false;
        dispatcher.RegisterHandler<PingCommand>((_, _) =>
        {
            invoked = true;
            return Task.FromResult(CommandDispatchResult.Ok());
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(new PingCommand(1), cts.Token));

        Assert.False(invoked);
    }

    [Fact]
    public async Task DispatchAsync_StructCommand_Works()
    {
        var dispatcher = new InMemoryCommandDispatcher();
        dispatcher.RegisterHandler(new PingCommandHandler());

        var result = await dispatcher.DispatchAsync(new PingCommand(42));

        Assert.True(result.Success);
        Assert.Equal("pong:42", result.Message);
    }
}
