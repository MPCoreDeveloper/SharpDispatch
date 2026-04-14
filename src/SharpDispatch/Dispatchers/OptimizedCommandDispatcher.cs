// <copyright file="OptimizedCommandDispatcher.cs" company="MPCoreDeveloper">
// Copyright (c) 2026 MPCoreDeveloper and GitHub Copilot. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace SharpDispatch;

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// High-performance command dispatcher backed by a <see cref="FrozenDictionary{TKey,TValue}"/>
/// and pre-built typed delegates that eliminate per-call DI overhead.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hot-path breakdown (singleton handler, struct command):</b><br/>
/// <c>FrozenDictionary.TryGetValue</c> (~5 ns) →
/// JIT-specialised type check (~1 ns) →
/// delegate invoke (~1 ns) →
/// <c>handler.HandleAsync</c> (application code).<br/>
/// Target overhead: <b>~10–15 ns, 0 allocations</b>.
/// </para>
/// <para>
/// <b>Two construction paths:</b>
/// <list type="bullet">
///   <item>
///     <b>AOT-safe (recommended):</b>
///     <see cref="DispatchServiceCollectionExtensions.AddOptimizedCommandDispatcher(IServiceCollection,Action{CommandDispatcherBuilder})"/>
///     — uses <see cref="CommandDispatcherBuilder"/> to construct invokers without any runtime reflection.
///     No <c>MakeGenericType</c>, no <c>Activator.CreateInstance</c>, no <see cref="IServiceCollection"/>
///     closure held after startup.
///   </item>
///   <item>
///     <b>Scan-based (backwards-compatible):</b>
///     <see cref="DispatchServiceCollectionExtensions.AddOptimizedCommandDispatcher(IServiceCollection)"/>
///     — scans the <see cref="IServiceCollection"/> at startup using reflection.
///     Annotated with <see cref="RequiresDynamicCodeAttribute"/>.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class OptimizedCommandDispatcher : ICommandDispatcher
{
    private readonly FrozenDictionary<Type, CommandInvoker> _invokers;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// <b>AOT-safe constructor.</b> Accepts a pre-built set of invokers from
    /// <see cref="CommandDispatcherBuilder.BuildInvokers"/>. No reflection involved.
    /// </summary>
    /// <param name="serviceProvider">Root service provider (for scoped/transient handler resolution).</param>
    /// <param name="invokers">
    /// Pre-built invoker table produced by <see cref="CommandDispatcherBuilder"/>.
    /// Singleton handlers are already resolved and baked into delegate closures.
    /// </param>
    internal OptimizedCommandDispatcher(
        IServiceProvider serviceProvider,
        FrozenDictionary<Type, CommandInvoker> invokers)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(invokers);

        _serviceProvider = serviceProvider;
        _invokers = invokers;
    }

    /// <summary>
    /// <b>Scan-based constructor.</b> Iterates <paramref name="services"/> to discover all
    /// <c>ICommandHandler&lt;TCommand&gt;</c> registrations and builds typed invokers via
    /// <see cref="Type.MakeGenericType"/>. One-time startup cost; hot path is identical.
    /// </summary>
    /// <param name="serviceProvider">Root service provider.</param>
    /// <param name="services">
    /// Service collection snapshot. All <c>ICommandHandler&lt;TCommand&gt;</c> descriptors
    /// present at this point are wired up. Last descriptor per command type wins.
    /// </param>
    [RequiresDynamicCode("Uses MakeGenericType to build typed invokers. Use AddOptimizedCommandDispatcher(cfg => ...) for AOT-safe registration.")]
    [RequiresUnreferencedCode("Scans IServiceCollection for ICommandHandler<T> registrations. Handler types must be preserved. Use AddOptimizedCommandDispatcher(cfg => ...) for AOT-safe registration.")]
    internal OptimizedCommandDispatcher(IServiceProvider serviceProvider, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(services);

        _serviceProvider = serviceProvider;

        var invokers = new Dictionary<Type, CommandInvoker>();

        foreach (var descriptor in services)
        {
            var serviceType = descriptor.ServiceType;
            if (!serviceType.IsGenericType ||
                serviceType.GetGenericTypeDefinition() != typeof(ICommandHandler<>))
            {
                continue;
            }

            var commandType = serviceType.GetGenericArguments()[0];

            // Last descriptor wins — consistent with IServiceProvider.GetService resolution order.
            invokers[commandType] = CommandInvoker.Create(commandType, descriptor.Lifetime, serviceProvider);
        }

        _invokers = invokers.ToFrozenDictionary();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Hot path: <see cref="FrozenDictionary{TKey,TValue}.TryGetValue"/> lookup followed by
    /// a JIT-specialised <c>is TypedCommandInvoker&lt;TCommand&gt;</c> check that avoids
    /// boxing when <typeparamref name="TCommand"/> is a value type (struct).
    /// </remarks>
    public Task<CommandDispatchResult> DispatchAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_invokers.TryGetValue(typeof(TCommand), out var invoker))
        {
            return Task.FromResult(
                CommandDispatchResult.Fail($"No handler registered for command '{typeof(TCommand).Name}'."));
        }

        // JIT specialises this check per TCommand — avoids boxing for struct commands.
        // The fallback branch is unreachable in normal operation (Create always returns TypedCommandInvoker<T>).
        if (invoker is TypedCommandInvoker<TCommand> typed)
            return typed.InvokeTyped(_serviceProvider, command, cancellationToken);

        return invoker.InvokeAsync(_serviceProvider, command, cancellationToken);
    }
}
