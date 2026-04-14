// <copyright file="CommandDispatcherBuilder.cs" company="MPCoreDeveloper">
// Copyright (c) 2026 MPCoreDeveloper and GitHub Copilot. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace SharpDispatch;

using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Fluent builder for <see cref="OptimizedCommandDispatcher"/> that registers handlers
/// without any runtime reflection — fully Native AOT-safe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> the scan-based <see cref="DispatchServiceCollectionExtensions.AddOptimizedCommandDispatcher(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// overload uses <see cref="Type.MakeGenericType"/> and <c>Activator.CreateInstance</c>
/// at startup, which is incompatible with Native AOT trimming. This builder avoids all
/// runtime reflection by constructing <see cref="TypedCommandInvoker{TCommand}"/> directly
/// via explicit generic instantiation — the AOT compiler can see every concrete type.
/// </para>
/// <para>
/// <b>Trade-offs eliminated compared to the scan-based path:</b>
/// <list type="bullet">
///   <item>No <c>MakeGenericType</c> / <c>Activator.CreateInstance</c> → Native AOT-safe.</item>
///   <item>No <see cref="IServiceCollection"/> closure held after startup → no extra memory.</item>
///   <item>No full descriptor scan → O(n handlers) instead of O(all DI registrations).</item>
///   <item>Registration order does not matter; last <c>AddHandler</c> for a type wins.</item>
/// </list>
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddOptimizedCommandDispatcher(cfg =>
/// {
///     cfg.AddHandler&lt;CreateOrderCommand, CreateOrderCommandHandler&gt;();
///     cfg.AddHandler&lt;CancelOrderCommand, CancelOrderCommandHandler&gt;();
///     cfg.AddHandler&lt;ShipOrderCommand,  ShipOrderCommandHandler,  ServiceLifetime.Scoped&gt;();
/// });
/// </code>
/// </para>
/// </remarks>
public sealed class CommandDispatcherBuilder
{
    // Stores per-command: DI descriptor + AOT-safe invoker factory.
    private readonly record struct Entry(
        Type CommandType,
        ServiceDescriptor HandlerDescriptor,
        Func<IServiceProvider, CommandInvoker> InvokerFactory);

    private readonly List<Entry> _entries = [];

    /// <summary>
    /// Registers a command handler.
    /// </summary>
    /// <typeparam name="TCommand">Command type. May be a class or a struct.</typeparam>
    /// <typeparam name="THandler">Handler implementation. Resolved from DI.</typeparam>
    /// <param name="lifetime">
    /// Service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.
    /// Singleton handlers are resolved once at startup and baked into a delegate closure
    /// (zero DI overhead per dispatch). Scoped/transient handlers are resolved per call.
    /// </param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <remarks>
    /// This method is AOT-safe: <c>new TypedCommandInvoker&lt;TCommand&gt;(...)</c> is an
    /// explicit generic instantiation visible to the AOT compiler — no
    /// <see cref="Type.MakeGenericType"/> or <c>Activator.CreateInstance</c> involved.
    /// </remarks>
    public CommandDispatcherBuilder AddHandler<TCommand, THandler>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        _entries.Add(new Entry(
            CommandType: typeof(TCommand),
            HandlerDescriptor: ServiceDescriptor.Describe(
                typeof(ICommandHandler<TCommand>), typeof(THandler), lifetime),
            // AOT-safe: explicit generic, no runtime type construction
            InvokerFactory: sp => new TypedCommandInvoker<TCommand>(lifetime, sp)));

        return this;
    }

    /// <summary>
    /// Returns DI descriptors for all registered handlers.
    /// Used by <see cref="DispatchServiceCollectionExtensions"/> to register handlers
    /// in the DI container alongside the dispatcher.
    /// </summary>
    internal IEnumerable<ServiceDescriptor> HandlerDescriptors =>
        _entries.Select(static e => e.HandlerDescriptor);

    /// <summary>
    /// Builds the <see cref="FrozenDictionary{TKey,TValue}"/> of pre-wired invokers.
    /// Called once when the dispatcher singleton is first resolved.
    /// </summary>
    internal FrozenDictionary<Type, CommandInvoker> BuildInvokers(IServiceProvider serviceProvider)
    {
        var dict = new Dictionary<Type, CommandInvoker>(_entries.Count);

        foreach (var entry in _entries)
        {
            // Last AddHandler call for the same TCommand wins —
            // consistent with IServiceProvider.GetService resolution order.
            dict[entry.CommandType] = entry.InvokerFactory(serviceProvider);
        }

        return dict.ToFrozenDictionary();
    }
}
