using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SharpDispatch.Tests;

// Types used by the TryDecorate tests.
public interface IMessageService
{
    string? Last { get; }

    void Send(string message);
}

public sealed class MessageService : IMessageService
{
    public string? Last { get; private set; }

    public void Send(string message) => Last = message;
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class FixedClock : IClock
{
    public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

public sealed class TimestampedMessageService : IMessageService
{
    private readonly IMessageService _inner;
    private readonly IClock _clock;

    public TimestampedMessageService(IMessageService inner, IClock clock)
    {
        _inner = inner;
        _clock = clock;
    }

    public string? Last => _inner.Last;

    public void Send(string message) => _inner.Send($"{_clock.UtcNow:O} {message}");
}

public sealed class DispatchCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);
}

public sealed class RecordingCommandDispatcher : ICommandDispatcher
{
    private readonly ICommandDispatcher _inner;
    private readonly DispatchCounter _counter;

    public RecordingCommandDispatcher(ICommandDispatcher inner, DispatchCounter counter)
    {
        _inner = inner;
        _counter = counter;
    }

    public Task<CommandDispatchResult> DispatchAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        _counter.Increment();
        return _inner.DispatchAsync(command, cancellationToken);
    }
}

public class TryDecorateTests
{
    [Fact]
    public void Decorate_ConcreteTypeRegistration_WrapsAndCallsThrough()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<IClock, FixedClock>();
        services.TryDecorate<IMessageService, TimestampedMessageService>();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMessageService>();

        Assert.IsType<TimestampedMessageService>(service);
        service.Send("hello");
        Assert.Equal("2026-01-01T00:00:00.0000000Z hello", service.Last);
    }

    [Fact]
    public void Decorate_PreservesOriginalLifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageService, MessageService>();
        services.TryDecorate<IMessageService, TimestampedMessageService>();

        var descriptor = Assert.Single(services);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void Decorate_InstanceRegistration_WrapsExistingInstance()
    {
        var inner = new MessageService();
        var services = new ServiceCollection();
        services.AddSingleton<IMessageService>(inner);
        services.AddSingleton<IClock, FixedClock>();
        services.TryDecorate<IMessageService, TimestampedMessageService>();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMessageService>();

        service.Send("direct");

        Assert.Equal("2026-01-01T00:00:00.0000000Z direct", inner.Last);
    }

    [Fact]
    public void Decorate_NoRegistration_IsNoOp()
    {
        var services = new ServiceCollection();
        var countBefore = services.Count;

        services.TryDecorate<IMessageService, TimestampedMessageService>();

        Assert.Equal(countBefore, services.Count);

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IMessageService>());
    }

    [Fact]
    public async Task Decorate_ICommandDispatcher_WrapsDispatcher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DispatchCounter>();
        services.AddCommandHandler<PingCommand, PingCommandHandler>();
        services.AddCommandDispatcher();
        services.TryDecorate<ICommandDispatcher, RecordingCommandDispatcher>();

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();
        var counter = provider.GetRequiredService<DispatchCounter>();

        Assert.IsType<RecordingCommandDispatcher>(dispatcher);

        var result = await dispatcher.DispatchAsync(new PingCommand(1));

        Assert.True(result.Success);
        Assert.Equal("pong:1", result.Message);
        Assert.Equal(1, counter.Count);
    }
}
