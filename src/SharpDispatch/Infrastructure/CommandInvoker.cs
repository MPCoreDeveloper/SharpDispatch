// <copyright file="CommandInvoker.cs" company="MPCoreDeveloper">
// Copyright (c) 2026 MPCoreDeveloper and GitHub Copilot. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace SharpDispatch;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Base class for pre-built command handler invocation wrappers used by
/// <see cref="OptimizedCommandDispatcher"/>. All reflection occurs once at startup;
/// the dispatch hot path is allocation-free for singleton handlers.
/// </summary>
internal abstract class CommandInvoker
{
    /// <summary>
    /// Creates a <see cref="TypedCommandInvoker{TCommand}"/> for <paramref name="commandType"/>
    /// using one-time-at-startup reflection via <see cref="Type.MakeGenericType"/>.
    /// Only used by the scan-based <see cref="OptimizedCommandDispatcher"/> constructor.
    /// Prefer <see cref="CommandDispatcherBuilder"/> to avoid this call entirely.
    /// </summary>
    [RequiresDynamicCode("Uses MakeGenericType to build typed invokers. Use CommandDispatcherBuilder for AOT-safe registration.")]
    [RequiresUnreferencedCode("Reflects over ICommandHandler<T> registrations. Handler types must be preserved. Use CommandDispatcherBuilder for AOT-safe registration.")]
    internal static CommandInvoker Create(
        Type commandType,
        ServiceLifetime lifetime,
        IServiceProvider rootProvider)
    {
        var invokerType = typeof(TypedCommandInvoker<>).MakeGenericType(commandType);
        return (CommandInvoker)Activator.CreateInstance(invokerType, lifetime, rootProvider)!;
    }

    /// <summary>
    /// Invokes the handler with a boxed command instance.
    /// For value-type (struct) commands prefer the JIT-specialised path via
    /// <see cref="TypedCommandInvoker{TCommand}.InvokeTyped"/> to avoid boxing.
    /// </summary>
    internal abstract Task<CommandDispatchResult> InvokeAsync(
        IServiceProvider serviceProvider,
        object command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Strongly-typed command invoker that eliminates per-call DI overhead.
/// </summary>
/// <remarks>
/// <para>
/// <b>Singleton handlers</b> are resolved once during construction and baked into a
/// delegate closure — the handler reference is captured and <see cref="IServiceProvider"/>
/// is never consulted at dispatch time.
/// </para>
/// <para>
/// <b>Scoped / transient handlers</b> are resolved from the supplied
/// <see cref="IServiceProvider"/> on every call, mirroring the behaviour of
/// <see cref="ServiceProviderCommandDispatcher"/>.
/// </para>
/// <para>
/// The <see cref="InvokeTyped"/> overload accepts the concrete <typeparamref name="TCommand"/>
/// directly, which the JIT specialises per command type and therefore avoids boxing when
/// <typeparamref name="TCommand"/> is a value type (struct).
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The command type this invoker handles.</typeparam>
internal sealed class TypedCommandInvoker<TCommand> : CommandInvoker
    where TCommand : ICommand
{
    private readonly Func<IServiceProvider, TCommand, CancellationToken, Task<CommandDispatchResult>> _invoke;

    /// <summary>
    /// Initializes a new <see cref="TypedCommandInvoker{TCommand}"/>.
    /// Constructor is public so <c>Activator.CreateInstance</c> can reach it from
    /// <see cref="CommandInvoker.Create"/>; the class itself is internal.
    /// AOT-safe when instantiated directly via <see cref="CommandDispatcherBuilder"/>.
    /// </summary>
    public TypedCommandInvoker(ServiceLifetime lifetime, IServiceProvider rootProvider)
    {
        if (lifetime == ServiceLifetime.Singleton)
        {
            // Resolve once; bake handler reference into the closure.
            // After this point, IServiceProvider is never touched in the hot path.
            var handler = rootProvider.GetRequiredService<ICommandHandler<TCommand>>();
            _invoke = (_, cmd, ct) => handler.HandleAsync(cmd, ct);
        }
        else
        {
            // Per-call resolution keeps scoped / transient lifetime semantics correct.
            _invoke = static (sp, cmd, ct) =>
                sp.GetRequiredService<ICommandHandler<TCommand>>().HandleAsync(cmd, ct);
        }
    }

    /// <summary>
    /// Hot-path invocation with the concrete command type — no boxing for value-type commands.
    /// Called from the JIT-specialised <see cref="OptimizedCommandDispatcher.DispatchAsync{TCommand}"/>.
    /// </summary>
    internal Task<CommandDispatchResult> InvokeTyped(
        IServiceProvider serviceProvider,
        TCommand command,
        CancellationToken cancellationToken)
        => _invoke(serviceProvider, command, cancellationToken);

    /// <inheritdoc />
    internal override Task<CommandDispatchResult> InvokeAsync(
        IServiceProvider serviceProvider,
        object command,
        CancellationToken cancellationToken)
        => _invoke(serviceProvider, (TCommand)command, cancellationToken);
}
