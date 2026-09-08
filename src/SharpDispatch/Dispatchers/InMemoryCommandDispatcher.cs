// <copyright file="InMemoryCommandDispatcher.cs" company="MPCoreDeveloper">
// Copyright (c) 2026 MPCoreDeveloper and GitHub Copilot. All rights reserved.
// Licensed under the MIT License.
// </copyright>

namespace SharpDispatch;

using System.Collections.Concurrent;

/// <summary>
/// In-memory command dispatcher using explicit handler registration.
/// Ideal for unit testing and scenarios where DI is unavailable.
/// </summary>
public sealed class InMemoryCommandDispatcher : ICommandDispatcher
{
    private readonly ConcurrentDictionary<Type, object> _handlers = [];

    /// <summary>
    /// Registers a command handler.
    /// </summary>
    /// <typeparam name="TCommand">Command type.</typeparam>
    /// <param name="handler">Handler instance.</param>
    public void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[typeof(TCommand)] = handler;
    }

    /// <summary>
    /// Registers a delegate-based command handler.
    /// </summary>
    /// <typeparam name="TCommand">Command type.</typeparam>
    /// <param name="handler">Handler delegate receiving the command and a cancellation token.</param>
    public void RegisterHandler<TCommand>(Func<TCommand, CancellationToken, Task<CommandDispatchResult>> handler)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(handler);
        RegisterHandler(new DelegateCommandHandler<TCommand>(handler));
    }

    /// <summary>
    /// Registers a delegate-based command handler that does not observe cancellation.
    /// </summary>
    /// <typeparam name="TCommand">Command type.</typeparam>
    /// <param name="handler">Handler delegate receiving the command.</param>
    public void RegisterHandler<TCommand>(Func<TCommand, Task<CommandDispatchResult>> handler)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(handler);
        RegisterHandler(new DelegateCommandHandler<TCommand>((command, _) => handler(command)));
    }

    /// <summary>
    /// Gets the number of registered command handlers.
    /// </summary>
    public int HandlerCount => _handlers.Count;

    /// <summary>
    /// Determines whether a handler is registered for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">Command type.</typeparam>
    /// <returns><see langword="true"/> when a handler is registered; otherwise <see langword="false"/>.</returns>
    public bool ContainsHandler<TCommand>()
        where TCommand : ICommand
        => _handlers.ContainsKey(typeof(TCommand));

    /// <inheritdoc />
    public Task<CommandDispatchResult> DispatchAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_handlers.TryGetValue(typeof(TCommand), out var handlerObject) || handlerObject is not ICommandHandler<TCommand> handler)
        {
            return Task.FromResult(CommandDispatchResult.Fail($"No handler registered for command '{typeof(TCommand).Name}'."));
        }

        return handler.HandleAsync(command, cancellationToken);
    }

    private sealed class DelegateCommandHandler<TCommand>(
        Func<TCommand, CancellationToken, Task<CommandDispatchResult>> handler) : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public Task<CommandDispatchResult> HandleAsync(
            TCommand command,
            CancellationToken cancellationToken)
            => handler(command, cancellationToken);
    }
}
