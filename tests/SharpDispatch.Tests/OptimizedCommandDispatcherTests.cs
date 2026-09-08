using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SharpDispatch.Tests;

public class OptimizedCommandDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_BuilderConfiguredHandler_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddOptimizedCommandDispatcher(cfg =>
            cfg.AddHandler<CreateUserCommand, CreateUserCommandHandler>());
        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        Assert.IsType<OptimizedCommandDispatcher>(dispatcher);

        var result = await dispatcher.DispatchAsync(new CreateUserCommand("irene", 24));

        Assert.True(result.Success);
        Assert.Contains("irene", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_SingletonHandler_IsResolvedOnce()
    {
        var services = new ServiceCollection();
        services.AddSingleton<InstanceTracker>();
        services.AddOptimizedCommandDispatcher(cfg =>
            cfg.AddHandler<CreateUserCommand, TrackedCreateUserHandler>());
        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
        var tracker = provider.GetRequiredService<InstanceTracker>();

        await dispatcher.DispatchAsync(new CreateUserCommand("jane", 25));
        await dispatcher.DispatchAsync(new CreateUserCommand("kyle", 26));

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public async Task DispatchAsync_TransientHandler_IsResolvedPerDispatch()
    {
        var services = new ServiceCollection();
        services.AddSingleton<InstanceTracker>();
        services.AddOptimizedCommandDispatcher(cfg =>
            cfg.AddHandler<CreateUserCommand, TrackedCreateUserHandler>(
                ServiceLifetime.Transient));
        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
        var tracker = provider.GetRequiredService<InstanceTracker>();

        await dispatcher.DispatchAsync(new CreateUserCommand("liam", 27));
        await dispatcher.DispatchAsync(new CreateUserCommand("mia", 28));

        Assert.Equal(2, tracker.Count);
    }

    [Fact]
    public async Task DispatchAsync_MissingHandler_ReturnsFailure()
    {
        var services = new ServiceCollection();
        services.AddOptimizedCommandDispatcher(cfg => { /* no handlers */ });
        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new CreateUserCommand("noah", 1));

        Assert.False(result.Success);
        Assert.Equal("No handler registered for command 'CreateUserCommand'.", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_StructCommand_UsesTypedInvokerPath()
    {
        var services = new ServiceCollection();
        services.AddOptimizedCommandDispatcher(cfg =>
            cfg.AddHandler<PingCommand, PingCommandHandler>());
        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new PingCommand(11));

        Assert.True(result.Success);
        Assert.Equal("pong:11", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_CancelledToken_ThrowsBeforeHandlerInvocation()
    {
        var services = new ServiceCollection();
        services.AddOptimizedCommandDispatcher(cfg =>
            cfg.AddHandler<PingCommand, PingCommandHandler>());
        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(new PingCommand(1), cts.Token));
    }

    [Fact]
    public async Task ScanBasedOverload_DiscoversRegisteredHandlers()
    {
        var services = new ServiceCollection();
        services.AddCommandHandler<PingCommand, PingCommandHandler>();
        services.AddOptimizedCommandDispatcher();
        using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        Assert.IsType<OptimizedCommandDispatcher>(dispatcher);

        var result = await dispatcher.DispatchAsync(new PingCommand(5));

        Assert.True(result.Success);
        Assert.Equal("pong:5", result.Message);
    }
}
