using MacroNex.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace MacroNex.Presentation.Services;

/// <summary>
/// Bridges domain safety events to WPF UI (authorization prompts).
/// </summary>
public sealed class SafetyUiCoordinator : IHostedService
{
    private readonly ISafetyService _safetyService;
    private readonly ILogger<SafetyUiCoordinator> _logger;

    public SafetyUiCoordinator(ISafetyService safetyService, ILogger<SafetyUiCoordinator> logger)
    {
        _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _safetyService.AuthorizationRequired += OnAuthorizationRequired;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _safetyService.AuthorizationRequired -= OnAuthorizationRequired;
        return Task.CompletedTask;
    }

    private void OnAuthorizationRequired(object? sender, AuthorizationRequiredEventArgs e)
    {
        try
        {
            // Ensure we prompt on the UI thread.
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                e.ResultCallback.TrySetResult(AuthorizationResult.Denied("No UI dispatcher"));
                return;
            }

            dispatcher.Invoke(() =>
            {
                var message =
                    $"Dangerous operation requires authorization:\n\n" +
                    $"- Type: {e.Operation.Type}\n" +
                    $"- Description: {e.Operation.Description}\n\n" +
                    $"Script: {e.Context.Script.Name}\n" +
                    $"Command Index: {e.Context.CommandIndex}\n\n" +
                    $"Allow?";

                var result = MessageBox.Show(message, "Authorization Required", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    e.ResultCallback.TrySetResult(AuthorizationResult.Authorized("User approved", rememberDecision: true));
                }
                else
                {
                    e.ResultCallback.TrySetResult(AuthorizationResult.Denied("User denied"));
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authorization prompt failed");
            try { e.ResultCallback.TrySetResult(AuthorizationResult.Denied("Authorization UI failed")); } catch { }
        }
    }
}

