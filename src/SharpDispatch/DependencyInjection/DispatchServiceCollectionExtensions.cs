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
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICommandHandler<TCommand>, THandler>();
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
}
