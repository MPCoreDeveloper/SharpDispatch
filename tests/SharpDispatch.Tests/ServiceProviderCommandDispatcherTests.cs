using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SharpDispatch.Tests;

public class ServiceProviderCommandDispatcherTests
{
    private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.AddCommandDispatcher();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task DispatchAsync_ResolvesRegisteredHandler_AndSucceeds()
    {
        using var provider = Build(
            s => s.AddCommandHandler<CreateUserCommand, CreateUserCommandHandler>());
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new CreateUserCommand("carol", 28));

        Assert.True(result.Success);
        Assert.Contains("carol", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_MissingHandler_ReturnsFailure()
    {
        using var provider = Build();
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new CreateUserCommand("dave", 1));

        Assert.False(result.Success);
        Assert.Equal("No handler registered for command 'CreateUserCommand'.", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_SingletonHandler_IsResolvedOnce()
    {
        using var provider = Build(s =>
        {
            s.AddSingleton<InstanceTracker>();
            s.AddCommandHandler<CreateUserCommand, TrackedCreateUserHandler>();
        });
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
        var tracker = provider.GetRequiredService<InstanceTracker>();

        await dispatcher.DispatchAsync(new CreateUserCommand("eve", 20));
        await dispatcher.DispatchAsync(new CreateUserCommand("frank", 21));

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public async Task DispatchAsync_TransientHandler_IsResolvedPerDispatch()
    {
        using var provider = Build(s =>
        {
            s.AddSingleton<InstanceTracker>();
            s.AddCommandHandler<CreateUserCommand, TrackedCreateUserHandler>(
                ServiceLifetime.Transient);
        });
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
        var tracker = provider.GetRequiredService<InstanceTracker>();

        await dispatcher.DispatchAsync(new CreateUserCommand("grace", 22));
        await dispatcher.DispatchAsync(new CreateUserCommand("henry", 23));

        Assert.Equal(2, tracker.Count);
    }

    [Fact]
    public async Task DispatchAsync_StructCommand_Works()
    {
        using var provider = Build(s => s.AddCommandHandler<PingCommand, PingCommandHandler>());
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new PingCommand(9));

        Assert.True(result.Success);
        Assert.Equal("pong:9", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_CancelledToken_ThrowsBeforeHandlerInvocation()
    {
        using var provider = Build(s => s.AddCommandHandler<PingCommand, PingCommandHandler>());
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(new PingCommand(1), cts.Token));
    }
}
