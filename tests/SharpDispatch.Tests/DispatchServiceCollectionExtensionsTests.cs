using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SharpDispatch.Tests;

public class DispatchServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCommandHandler_DefaultLifetime_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddCommandHandler<CreateUserCommand, CreateUserCommandHandler>();

        var descriptor = Assert.Single(services);
        Assert.Equal(typeof(ICommandHandler<CreateUserCommand>), descriptor.ServiceType);
        Assert.Equal(typeof(CreateUserCommandHandler), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddCommandHandler_WithLifetime_UsesRequestedLifetime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddCommandHandler<CreateUserCommand, CreateUserCommandHandler>(lifetime);

        var descriptor = Assert.Single(services);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void AddCommandDispatcher_RegistersServiceProviderDispatcherAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddCommandDispatcher();

        var descriptor = Assert.Single(services);
        Assert.Equal(typeof(ICommandDispatcher), descriptor.ServiceType);
        Assert.Equal(typeof(ServiceProviderCommandDispatcher), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddCommandDispatcher_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddCommandDispatcher();
        services.AddCommandDispatcher();

        Assert.Single(services);
    }

    [Fact]
    public void AddOptimizedCommandDispatcher_ReplacesExistingDispatcher()
    {
        var services = new ServiceCollection();
        services.AddCommandDispatcher();
        services.AddOptimizedCommandDispatcher(cfg =>
            cfg.AddHandler<CreateUserCommand, CreateUserCommandHandler>());

        var dispatcherDescriptors =
            services.Where(d => d.ServiceType == typeof(ICommandDispatcher)).ToArray();
        var dispatcherDescriptor = Assert.Single(dispatcherDescriptors);
        Assert.Equal(ServiceLifetime.Singleton, dispatcherDescriptor.Lifetime);
        Assert.Null(dispatcherDescriptor.ImplementationType);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<OptimizedCommandDispatcher>(
            provider.GetRequiredService<ICommandDispatcher>());
    }

    [Fact]
    public void AddOptimizedCommandDispatcher_BuilderRegistersHandlerDescriptors()
    {
        var services = new ServiceCollection();
        services.AddOptimizedCommandDispatcher(cfg =>
            cfg.AddHandler<CreateUserCommand, CreateUserCommandHandler>());

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ICommandHandler<CreateUserCommand>)
              && d.ImplementationType == typeof(CreateUserCommandHandler)
              && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddCommandHandler_NullServices_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddCommandHandler<CreateUserCommand, CreateUserCommandHandler>());

    [Fact]
    public void AddCommandDispatcher_NullServices_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddCommandDispatcher());
}
