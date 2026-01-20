using MacroStudio.Domain.Events;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Base interface for domain event handlers.
/// </summary>
/// <typeparam name="TEventArgs">Type of event arguments this handler processes.</typeparam>
public interface IDomainEventHandler<in TEventArgs> where TEventArgs : DomainEventArgs
{
    /// <summary>
    /// Handles the specified domain event.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TEventArgs eventArgs, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for publishing domain events to registered handlers.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes a domain event to all registered handlers.
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event arguments.</typeparam>
    /// <param name="eventArgs">The event arguments to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync<TEventArgs>(TEventArgs eventArgs, CancellationToken cancellationToken = default) where TEventArgs : DomainEventArgs;

    /// <summary>
    /// Registers an event handler for a specific event type.
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event arguments.</typeparam>
    /// <param name="handler">The event handler to register.</param>
    void RegisterHandler<TEventArgs>(IDomainEventHandler<TEventArgs> handler) where TEventArgs : DomainEventArgs;

    /// <summary>
    /// Unregisters an event handler for a specific event type.
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event arguments.</typeparam>
    /// <param name="handler">The event handler to unregister.</param>
    void UnregisterHandler<TEventArgs>(IDomainEventHandler<TEventArgs> handler) where TEventArgs : DomainEventArgs;

    /// <summary>
    /// Gets the number of registered handlers for a specific event type.
    /// </summary>
    /// <typeparam name="TEventArgs">Type of event arguments.</typeparam>
    /// <returns>Number of registered handlers.</returns>
    int GetHandlerCount<TEventArgs>() where TEventArgs : DomainEventArgs;
}

/// <summary>
/// Specific event handler interfaces for different domain events.
/// </summary>

/// <summary>
/// Handler for command recorded events.
/// </summary>
public interface ICommandRecordedEventHandler : IDomainEventHandler<CommandRecordedEventArgs>
{
}

/// <summary>
/// Handler for recording state changed events.
/// </summary>
public interface IRecordingStateChangedEventHandler : IDomainEventHandler<RecordingStateChangedEventArgs>
{
}

/// <summary>
/// Handler for recording error events.
/// </summary>
public interface IRecordingErrorEventHandler : IDomainEventHandler<RecordingErrorEventArgs>
{
}

/// <summary>
/// Handler for execution progress events.
/// </summary>
public interface IExecutionProgressEventHandler : IDomainEventHandler<ExecutionProgressEventArgs>
{
}

/// <summary>
/// Handler for execution state changed events.
/// </summary>
public interface IExecutionStateChangedEventHandler : IDomainEventHandler<ExecutionStateChangedEventArgs>
{
}

/// <summary>
/// Handler for command executing events.
/// </summary>
public interface ICommandExecutingEventHandler : IDomainEventHandler<CommandExecutingEventArgs>
{
}

/// <summary>
/// Handler for command executed events.
/// </summary>
public interface ICommandExecutedEventHandler : IDomainEventHandler<CommandExecutedEventArgs>
{
}

/// <summary>
/// Handler for execution error events.
/// </summary>
public interface IExecutionErrorEventHandler : IDomainEventHandler<ExecutionErrorEventArgs>
{
}

/// <summary>
/// Handler for execution completed events.
/// </summary>
public interface IExecutionCompletedEventHandler : IDomainEventHandler<ExecutionCompletedEventArgs>
{
}