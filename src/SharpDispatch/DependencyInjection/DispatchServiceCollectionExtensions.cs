// <copyright file="DispatchServiceCollectionExtensions.cs" company="MPCoreDeveloper">
// Copyright (c) 2026 MPCoreDeveloper and GitHub Copilot. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace SharpDispatch;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection extensions for command dispatching.
/// Included in both <c>SharpDispatch</c> (standalone) and
/// <c>SharpCoreDB.CQRS</c> (full EventSourcing integration).
/// </summary>
public static class DispatchServiceCollectionExtensions
{
    /// <summary>
    /// Registers a command handler in the DI container.
    /// Compatible with all three dispatcher implementations.
    /// </summary>
    /// <typeparam name="TCommand">Command type.</typeparam>
    /// <typeparam name="THandler">Handler implementation type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddCommandHandler<TCommand, THandler>(
        this IServiceCollection services)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
        => services.AddCommandHandler<TCommand, THandler>(ServiceLifetime.Singleton);

    /// <summary>
    /// Registers a command handler in the DI container with the specified lifetime.
    /// Compatible with all three dispatcher implementations.
    /// </summary>
    /// <typeparam name="TCommand">Command type.</typeparam>
    /// <typeparam name="THandler">Handler implementation type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="lifetime">
    /// Desired service lifetime. Prefer <see cref="ServiceLifetime.Singleton"/> for stateless
    /// handlers; use scoped or transient lifetimes when a handler requires per-operation state.
    /// </param>
    /// <returns>Service collection for chaining.</returns>
    /// <remarks>
    /// Scoped and transient handlers are resolved on every dispatch from the provider supplied
    /// to the dispatcher. When the dispatcher is registered as a singleton, scoped handlers are
    /// resolved from the root scope — see the documentation for scope-aware alternatives.
    /// </remarks>
    public static IServiceCollection AddCommandHandler<TCommand, THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(ServiceDescriptor.Describe(
            typeof(ICommandHandler<TCommand>),
            typeof(THandler),
            lifetime));
        return services;
    }

    /// <summary>
    /// Registers <see cref="ServiceProviderCommandDispatcher"/> as the
    /// <see cref="ICommandDispatcher"/> implementation.
    /// Use this when you do not need the performance benefits of
    /// <see cref="OptimizedCommandDispatcher"/> and want the simplest setup.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddCommandDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ICommandDispatcher, ServiceProviderCommandDispatcher>();
        return services;
    }

    // ── OptimizedCommandDispatcher ────────────────────────────────────────────

    /// <summary>
    /// <b>[AOT-safe]</b> Registers <see cref="OptimizedCommandDispatcher"/> using a fluent
    /// <see cref="CommandDispatcherBuilder"/> that pre-wires handlers without any runtime reflection.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">
    /// Builder callback. Register every command handler with
    /// <c>cfg.AddHandler&lt;TCommand, THandler&gt;()</c>. Handler implementations are
    /// automatically added to the DI container with the specified lifetime (default: Singleton).
    /// </param>
    /// <returns>Service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <b>This is the recommended overload</b> for all new code and AOT/trimmed deployments.
    /// </para>
    /// <para>
    /// <b>Trade-offs eliminated vs. the scan-based overload:</b>
    /// <list type="bullet">
    ///   <item>✅ No <c>MakeGenericType</c> / <c>Activator.CreateInstance</c> → Native AOT-safe.</item>
    ///   <item>✅ No <see cref="IServiceCollection"/> reference held after startup.</item>
    ///   <item>✅ O(n handlers) startup scan instead of O(all DI registrations).</item>
    ///   <item>✅ Explicit, compile-time-visible handler registration.</item>
    /// </list>
    /// </para>
    /// <example>
    /// <code>
    /// services.AddOptimizedCommandDispatcher(cfg =>
    /// {
    ///     cfg.AddHandler&lt;CreateOrderCommand, CreateOrderCommandHandler&gt;();
    ///     cfg.AddHandler&lt;CancelOrderCommand, CancelOrderCommandHandler&gt;();
    ///     cfg.AddHandler&lt;ShipOrderCommand,   ShipOrderCommandHandler, ServiceLifetime.Scoped&gt;();
    /// });
    /// </code>
    /// </example>
    /// </remarks>
    public static IServiceCollection AddOptimizedCommandDispatcher(
        this IServiceCollection services,
        Action<CommandDispatcherBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new CommandDispatcherBuilder();
        configure(builder);

        // Register handler implementations in DI (for consumers that resolve ICommandHandler<T> directly).
        foreach (var descriptor in builder.HandlerDescriptors)
            services.TryAdd(descriptor);

        // Capture only 'builder' — not IServiceCollection — no reference cycle, no extra memory.
        services.Replace(ServiceDescriptor.Singleton<ICommandDispatcher>(
            sp => new OptimizedCommandDispatcher(sp, builder.BuildInvokers(sp))));

        return services;
    }

    /// <summary>
    /// <b>[Reflection / scan-based]</b> Replaces the current <see cref="ICommandDispatcher"/>
    /// with <see cref="OptimizedCommandDispatcher"/> by scanning the
    /// <see cref="IServiceCollection"/> for all <c>ICommandHandler&lt;TCommand&gt;</c>
    /// registrations at startup.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Call this method <em>after</em> all <c>AddCommandHandler</c> calls and
    /// <em>before</em> <c>BuildServiceProvider</c>.
    /// </para>
    /// <para>
    /// <b>Prefer <see cref="AddOptimizedCommandDispatcher(IServiceCollection,Action{CommandDispatcherBuilder})"/>
    /// for new code.</b> This scan-based overload exists for backwards compatibility and scenarios
    /// where you cannot enumerate handlers at registration time.
    /// </para>
    /// <para>
    /// <b>Known limitations (eliminated by the builder overload):</b>
    /// <list type="bullet">
    ///   <item>Uses <c>MakeGenericType</c> + <c>Activator.CreateInstance</c> → not Native AOT-safe.</item>
    ///   <item>Holds the <see cref="IServiceCollection"/> reference in a closure after startup.</item>
    ///   <item>Scans all DI registrations, not just command handlers.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [RequiresDynamicCode("Uses MakeGenericType to build typed invokers. Use AddOptimizedCommandDispatcher(cfg => ...) for AOT-safe registration.")]
    [RequiresUnreferencedCode("Scans IServiceCollection for ICommandHandler<T> registrations. Handler types must be preserved. Use AddOptimizedCommandDispatcher(cfg => ...) for AOT-safe registration.")]
    public static IServiceCollection AddOptimizedCommandDispatcher(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<ICommandDispatcher>(
            sp => new OptimizedCommandDispatcher(sp, services)));
        return services;
    }

    // ── Decorator support ─────────────────────────────────────────────────────

    /// <summary>
    /// Wraps the most recently registered <typeparamref name="TService"/> with
    /// <typeparamref name="TDecorator"/> (last registration wins, mirroring
    /// <see cref="IServiceProvider.GetService(Type)"/> resolution order).
    /// </summary>
    /// <typeparam name="TService">The service type to decorate.</typeparam>
    /// <typeparam name="TDecorator">
    /// The decorator type. Its constructor must accept an inner
    /// <typeparamref name="TService"/> instance (resolved from the original registration);
    /// any remaining constructor arguments are resolved from DI.
    /// </typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// No-op when no registration of <typeparamref name="TService"/> exists
    /// (the "<i>try</i>" semantics). The original service lifetime is preserved for
    /// the decorated registration.
    /// </para>
    /// <para>
    /// <b>Startup-time convenience only.</b> Decorators are resolved through
    /// <see cref="ActivatorUtilities"/>, so this helper is <em>not</em> Native AOT-safe.
    /// Prefer the scan-free <see cref="AddOptimizedCommandDispatcher(IServiceCollection, Action{CommandDispatcherBuilder})"/>
    /// registration path in AOT/trimmed deployments.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddCommandDispatcher();
    /// services.TryDecorate&lt;ICommandDispatcher, LoggingCommandDispatcher&gt;();
    /// </code>
    /// </example>
    [RequiresDynamicCode("Resolves the decorator and inner service constructors at runtime via ActivatorUtilities.")]
    [RequiresUnreferencedCode("Resolves the decorator and inner service constructors at runtime via ActivatorUtilities.")]
    public static IServiceCollection TryDecorate<TService, TDecorator>(
        this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        for (var i = services.Count - 1; i >= 0; i--)
        {
            var inner = services[i];
            if (inner.ServiceType != typeof(TService))
            {
                continue;
            }

            services.RemoveAt(i);
            services.Add(ServiceDescriptor.Describe(
                typeof(TService),
                sp => ActivatorUtilities.CreateInstance<TDecorator>(
                    sp,
                    ResolveService(sp, inner)),
                inner.Lifetime));

            return services;
        }

        return services;
    }

    private static object ResolveService(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        var implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException(
                $"Service '{descriptor.ServiceType}' has no resolvable implementation.");

        return ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
    }
}
