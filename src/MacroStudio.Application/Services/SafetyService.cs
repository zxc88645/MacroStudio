using MacroStudio.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MacroStudio.Application.Services;

/// <summary>
/// Application service implementing safety controls (kill switch, limits, authorization).
/// </summary>
public sealed class SafetyService : ISafetyService
{
    private readonly ILogger<SafetyService> _logger;
    private readonly object _lockObject = new();
    private bool _isKillSwitchActive;

    private readonly List<AuthorizedOperation> _authorizedOperations = new();

    public SafetyService(ILogger<SafetyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsKillSwitchActive
    {
        get { lock (_lockObject) return _isKillSwitchActive; }
        private set { lock (_lockObject) _isKillSwitchActive = value; }
    }

    public event EventHandler<KillSwitchActivatedEventArgs>? KillSwitchActivated;
    public event EventHandler<AuthorizationRequiredEventArgs>? AuthorizationRequired;

    public Task ActivateKillSwitchAsync(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            reason = "Kill switch activated";

        if (IsKillSwitchActive)
            return Task.CompletedTask;

        IsKillSwitchActive = true;
        _logger.LogWarning("Kill switch activated: {Reason}", reason);

        try { KillSwitchActivated?.Invoke(this, new KillSwitchActivatedEventArgs(reason)); }
        catch { }

        return Task.CompletedTask;
    }

    public Task DeactivateKillSwitchAsync()
    {
        if (!IsKillSwitchActive)
            return Task.CompletedTask;

        IsKillSwitchActive = false;
        _logger.LogInformation("Kill switch deactivated");
        return Task.CompletedTask;
    }

    public ExecutionLimitCheckResult CheckExecutionLimits(Domain.Entities.Script script, ExecutionLimits limits)
    {
        if (script == null) throw new ArgumentNullException(nameof(script));
        if (limits == null) throw new ArgumentNullException(nameof(limits));

        var violations = new List<LimitViolation>();

        var estimated = script.EstimatedDuration;

        if (script.CommandCount > limits.MaxCommandCount)
        {
            violations.Add(new LimitViolation(
                LimitType.CommandCount,
                limits.MaxCommandCount,
                script.CommandCount,
                $"Command count {script.CommandCount} exceeds max {limits.MaxCommandCount}"));
        }

        if (estimated > limits.MaxExecutionTime)
        {
            violations.Add(new LimitViolation(
                LimitType.ExecutionTime,
                limits.MaxExecutionTime,
                estimated,
                $"Estimated execution time {estimated} exceeds max {limits.MaxExecutionTime}"));
        }

        // Basic command delay enforcement
        var minDelayMs = limits.MinCommandDelay.TotalMilliseconds;
        if (minDelayMs > 0 && script.Commands.Any(c => c.Delay < limits.MinCommandDelay))
        {
            violations.Add(new LimitViolation(
                LimitType.CommandDelay,
                limits.MinCommandDelay,
                "Some commands have smaller delay",
                $"One or more commands have delay smaller than {limits.MinCommandDelay}"));
        }

        return violations.Count == 0
            ? ExecutionLimitCheckResult.WithinLimits(estimated)
            : ExecutionLimitCheckResult.ExceedsLimits(violations, estimated);
    }

    public async Task<AuthorizationResult> RequestAuthorizationAsync(DangerousOperation operation, AuthorizationContext context)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (IsOperationAuthorized(operation))
        {
            return AuthorizationResult.Authorized("Previously authorized", rememberDecision: true);
        }

        var args = new AuthorizationRequiredEventArgs(operation, context);

        try { AuthorizationRequired?.Invoke(this, args); }
        catch { }

        // If no UI handler is attached, default to deny for safety.
        var completed = await Task.WhenAny(args.ResultCallback.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        if (completed != args.ResultCallback.Task)
        {
            return AuthorizationResult.Denied("No authorization handler available");
        }

        var result = await args.ResultCallback.Task;
        if (result.IsAuthorized && result.RememberDecision)
        {
            lock (_lockObject)
            {
                _authorizedOperations.Add(new AuthorizedOperation(operation, DateTime.UtcNow, result.Reason, result.ExpiresAt));
            }
        }

        return result;
    }

    public bool IsOperationAuthorized(DangerousOperation operation)
    {
        if (operation == null) return false;
        lock (_lockObject)
        {
            return _authorizedOperations.Any(a => a.IsValid && a.Operation.Description == operation.Description && a.Operation.Type == operation.Type);
        }
    }

    public Task RevokeAuthorizationAsync(DangerousOperation operation)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        lock (_lockObject)
        {
            _authorizedOperations.RemoveAll(a => a.Operation.Description == operation.Description && a.Operation.Type == operation.Type);
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<AuthorizedOperation>> GetAuthorizedOperationsAsync()
    {
        lock (_lockObject)
        {
            return Task.FromResult<IEnumerable<AuthorizedOperation>>(_authorizedOperations.Where(a => a.IsValid).ToList());
        }
    }

    public Task ClearAllAuthorizationsAsync()
    {
        lock (_lockObject)
        {
            _authorizedOperations.Clear();
        }
        return Task.CompletedTask;
    }

    public Task<SafetyValidationResult> ValidateSafetySystemAsync()
    {
        // Basic validation placeholder (extend with hotkey readiness, etc.)
        return Task.FromResult(SafetyValidationResult.Success());
    }
}

