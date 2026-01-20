using MacroStudio.Domain.Entities;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for safety controls and authorization.
/// Manages kill switches, execution limits, and dangerous operation authorization.
/// </summary>
public interface ISafetyService
{
    /// <summary>
    /// Gets whether the kill switch is currently active.
    /// </summary>
    bool IsKillSwitchActive { get; }

    /// <summary>
    /// Event raised when the kill switch is activated.
    /// </summary>
    event EventHandler<KillSwitchActivatedEventArgs> KillSwitchActivated;

    /// <summary>
    /// Event raised when authorization is required for a dangerous operation.
    /// </summary>
    event EventHandler<AuthorizationRequiredEventArgs> AuthorizationRequired;

    /// <summary>
    /// Activates the kill switch, immediately terminating all automation.
    /// </summary>
    /// <param name="reason">Reason for kill switch activation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ActivateKillSwitchAsync(string reason);

    /// <summary>
    /// Deactivates the kill switch, allowing automation to resume.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeactivateKillSwitchAsync();

    /// <summary>
    /// Checks if execution limits would be exceeded by the given script.
    /// </summary>
    /// <param name="script">The script to check.</param>
    /// <param name="limits">Execution limits to enforce.</param>
    /// <returns>Result indicating if limits would be exceeded.</returns>
    ExecutionLimitCheckResult CheckExecutionLimits(Script script, ExecutionLimits limits);

    /// <summary>
    /// Requests authorization for a dangerous operation.
    /// </summary>
    /// <param name="operation">The dangerous operation requiring authorization.</param>
    /// <param name="context">Additional context for the authorization request.</param>
    /// <returns>A task representing the asynchronous operation. The task result indicates if authorization was granted.</returns>
    Task<AuthorizationResult> RequestAuthorizationAsync(DangerousOperation operation, AuthorizationContext context);

    /// <summary>
    /// Checks if a dangerous operation has been previously authorized.
    /// </summary>
    /// <param name="operation">The dangerous operation to check.</param>
    /// <returns>True if the operation has been authorized, false otherwise.</returns>
    bool IsOperationAuthorized(DangerousOperation operation);

    /// <summary>
    /// Revokes authorization for a previously authorized dangerous operation.
    /// </summary>
    /// <param name="operation">The operation to revoke authorization for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeAuthorizationAsync(DangerousOperation operation);

    /// <summary>
    /// Gets all currently authorized dangerous operations.
    /// </summary>
    /// <returns>A task representing the asynchronous operation. The task result contains authorized operations.</returns>
    Task<IEnumerable<AuthorizedOperation>> GetAuthorizedOperationsAsync();

    /// <summary>
    /// Clears all authorized operations (requires confirmation).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearAllAuthorizationsAsync();

    /// <summary>
    /// Validates safety configuration and system readiness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation. The task result contains the validation result.</returns>
    Task<SafetyValidationResult> ValidateSafetySystemAsync();
}

/// <summary>
/// Represents execution limits for safety enforcement.
/// </summary>
public class ExecutionLimits
{
    /// <summary>
    /// Maximum number of commands allowed in a single execution.
    /// </summary>
    public int MaxCommandCount { get; set; } = 10000;

    /// <summary>
    /// Maximum total execution time allowed.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum number of mouse clicks allowed.
    /// </summary>
    public int MaxMouseClicks { get; set; } = 5000;

    /// <summary>
    /// Maximum number of keyboard inputs allowed.
    /// </summary>
    public int MaxKeyboardInputs { get; set; } = 5000;

    /// <summary>
    /// Minimum delay required between commands.
    /// </summary>
    public TimeSpan MinCommandDelay { get; set; } = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Whether to enforce limits strictly (terminate on violation) or warn only.
    /// </summary>
    public bool StrictEnforcement { get; set; } = true;

    /// <summary>
    /// Creates default execution limits.
    /// </summary>
    /// <returns>A new ExecutionLimits instance with default values.</returns>
    public static ExecutionLimits Default() => new();

    /// <summary>
    /// Creates conservative execution limits for high-security environments.
    /// </summary>
    /// <returns>A new ExecutionLimits instance with conservative values.</returns>
    public static ExecutionLimits Conservative() => new()
    {
        MaxCommandCount = 1000,
        MaxExecutionTime = TimeSpan.FromMinutes(5),
        MaxMouseClicks = 500,
        MaxKeyboardInputs = 500,
        MinCommandDelay = TimeSpan.FromMilliseconds(10),
        StrictEnforcement = true
    };
}

/// <summary>
/// Result of execution limit checking.
/// </summary>
public class ExecutionLimitCheckResult
{
    /// <summary>
    /// Whether the script would exceed execution limits.
    /// </summary>
    public bool WouldExceedLimits { get; }

    /// <summary>
    /// List of limits that would be exceeded.
    /// </summary>
    public IReadOnlyList<LimitViolation> Violations { get; }

    /// <summary>
    /// Estimated execution time for the script.
    /// </summary>
    public TimeSpan EstimatedExecutionTime { get; }

    /// <summary>
    /// Initializes a new execution limit check result.
    /// </summary>
    /// <param name="wouldExceedLimits">Whether limits would be exceeded.</param>
    /// <param name="violations">List of limit violations.</param>
    /// <param name="estimatedExecutionTime">Estimated execution time.</param>
    public ExecutionLimitCheckResult(bool wouldExceedLimits, IEnumerable<LimitViolation> violations, TimeSpan estimatedExecutionTime)
    {
        WouldExceedLimits = wouldExceedLimits;
        Violations = violations.ToList().AsReadOnly();
        EstimatedExecutionTime = estimatedExecutionTime;
    }

    /// <summary>
    /// Creates a result indicating no limit violations.
    /// </summary>
    /// <param name="estimatedExecutionTime">Estimated execution time.</param>
    /// <returns>A result indicating the script is within limits.</returns>
    public static ExecutionLimitCheckResult WithinLimits(TimeSpan estimatedExecutionTime)
    {
        return new ExecutionLimitCheckResult(false, Array.Empty<LimitViolation>(), estimatedExecutionTime);
    }

    /// <summary>
    /// Creates a result indicating limit violations.
    /// </summary>
    /// <param name="violations">List of violations.</param>
    /// <param name="estimatedExecutionTime">Estimated execution time.</param>
    /// <returns>A result indicating the script exceeds limits.</returns>
    public static ExecutionLimitCheckResult ExceedsLimits(IEnumerable<LimitViolation> violations, TimeSpan estimatedExecutionTime)
    {
        return new ExecutionLimitCheckResult(true, violations, estimatedExecutionTime);
    }
}

/// <summary>
/// Represents a violation of execution limits.
/// </summary>
public class LimitViolation
{
    /// <summary>
    /// Type of limit that was violated.
    /// </summary>
    public LimitType LimitType { get; }

    /// <summary>
    /// The limit value that was exceeded.
    /// </summary>
    public object LimitValue { get; }

    /// <summary>
    /// The actual value that exceeded the limit.
    /// </summary>
    public object ActualValue { get; }

    /// <summary>
    /// Description of the violation.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new limit violation.
    /// </summary>
    /// <param name="limitType">Type of limit violated.</param>
    /// <param name="limitValue">The limit value.</param>
    /// <param name="actualValue">The actual value.</param>
    /// <param name="description">Description of the violation.</param>
    public LimitViolation(LimitType limitType, object limitValue, object actualValue, string description)
    {
        LimitType = limitType;
        LimitValue = limitValue ?? throw new ArgumentNullException(nameof(limitValue));
        ActualValue = actualValue ?? throw new ArgumentNullException(nameof(actualValue));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
}

/// <summary>
/// Types of execution limits.
/// </summary>
public enum LimitType
{
    /// <summary>
    /// Maximum command count limit.
    /// </summary>
    CommandCount,

    /// <summary>
    /// Maximum execution time limit.
    /// </summary>
    ExecutionTime,

    /// <summary>
    /// Maximum mouse clicks limit.
    /// </summary>
    MouseClicks,

    /// <summary>
    /// Maximum keyboard inputs limit.
    /// </summary>
    KeyboardInputs,

    /// <summary>
    /// Minimum command delay limit.
    /// </summary>
    CommandDelay
}

/// <summary>
/// Context information for authorization requests.
/// </summary>
public class AuthorizationContext
{
    /// <summary>
    /// The script containing the dangerous operation.
    /// </summary>
    public Script Script { get; }

    /// <summary>
    /// Index of the command in the script.
    /// </summary>
    public int CommandIndex { get; }

    /// <summary>
    /// Whether this is the first time this operation type is being requested.
    /// </summary>
    public bool IsFirstTime { get; }

    /// <summary>
    /// Additional context information.
    /// </summary>
    public Dictionary<string, object> Properties { get; }

    /// <summary>
    /// Initializes a new authorization context.
    /// </summary>
    /// <param name="script">The script containing the operation.</param>
    /// <param name="commandIndex">Index of the command.</param>
    /// <param name="isFirstTime">Whether this is the first time.</param>
    public AuthorizationContext(Script script, int commandIndex, bool isFirstTime)
    {
        Script = script ?? throw new ArgumentNullException(nameof(script));
        CommandIndex = commandIndex;
        IsFirstTime = isFirstTime;
        Properties = new Dictionary<string, object>();
    }
}

/// <summary>
/// Result of an authorization request.
/// </summary>
public class AuthorizationResult
{
    /// <summary>
    /// Whether authorization was granted.
    /// </summary>
    public bool IsAuthorized { get; }

    /// <summary>
    /// Reason for the authorization decision.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Whether to remember this authorization for future operations.
    /// </summary>
    public bool RememberDecision { get; }

    /// <summary>
    /// Expiration time for the authorization, if applicable.
    /// </summary>
    public DateTime? ExpiresAt { get; }

    /// <summary>
    /// Initializes a new authorization result.
    /// </summary>
    /// <param name="isAuthorized">Whether authorization was granted.</param>
    /// <param name="reason">Reason for the decision.</param>
    /// <param name="rememberDecision">Whether to remember the decision.</param>
    /// <param name="expiresAt">Expiration time, if applicable.</param>
    public AuthorizationResult(bool isAuthorized, string reason, bool rememberDecision = false, DateTime? expiresAt = null)
    {
        IsAuthorized = isAuthorized;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        RememberDecision = rememberDecision;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Creates an authorized result.
    /// </summary>
    /// <param name="reason">Reason for authorization.</param>
    /// <param name="rememberDecision">Whether to remember the decision.</param>
    /// <param name="expiresAt">Expiration time, if applicable.</param>
    /// <returns>An authorization result indicating approval.</returns>
    public static AuthorizationResult Authorized(string reason, bool rememberDecision = false, DateTime? expiresAt = null)
    {
        return new AuthorizationResult(true, reason, rememberDecision, expiresAt);
    }

    /// <summary>
    /// Creates a denied result.
    /// </summary>
    /// <param name="reason">Reason for denial.</param>
    /// <returns>An authorization result indicating denial.</returns>
    public static AuthorizationResult Denied(string reason)
    {
        return new AuthorizationResult(false, reason);
    }
}

/// <summary>
/// Represents an authorized dangerous operation.
/// </summary>
public class AuthorizedOperation
{
    /// <summary>
    /// The dangerous operation that was authorized.
    /// </summary>
    public DangerousOperation Operation { get; }

    /// <summary>
    /// When the authorization was granted.
    /// </summary>
    public DateTime AuthorizedAt { get; }

    /// <summary>
    /// When the authorization expires, if applicable.
    /// </summary>
    public DateTime? ExpiresAt { get; }

    /// <summary>
    /// Reason for granting authorization.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Whether the authorization is still valid.
    /// </summary>
    public bool IsValid => !ExpiresAt.HasValue || DateTime.UtcNow < ExpiresAt.Value;

    /// <summary>
    /// Initializes a new authorized operation.
    /// </summary>
    /// <param name="operation">The authorized operation.</param>
    /// <param name="authorizedAt">When authorization was granted.</param>
    /// <param name="reason">Reason for authorization.</param>
    /// <param name="expiresAt">Expiration time, if applicable.</param>
    public AuthorizedOperation(DangerousOperation operation, DateTime authorizedAt, string reason, DateTime? expiresAt = null)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        AuthorizedAt = authorizedAt;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        ExpiresAt = expiresAt;
    }
}

/// <summary>
/// Event arguments for kill switch activation.
/// </summary>
public class KillSwitchActivatedEventArgs : EventArgs
{
    /// <summary>
    /// Reason for kill switch activation.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Timestamp when the kill switch was activated.
    /// </summary>
    public DateTime ActivatedAt { get; }

    /// <summary>
    /// Initializes a new kill switch activated event.
    /// </summary>
    /// <param name="reason">Reason for activation.</param>
    public KillSwitchActivatedEventArgs(string reason)
    {
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        ActivatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for authorization required events.
/// </summary>
public class AuthorizationRequiredEventArgs : EventArgs
{
    /// <summary>
    /// The dangerous operation requiring authorization.
    /// </summary>
    public DangerousOperation Operation { get; }

    /// <summary>
    /// Context for the authorization request.
    /// </summary>
    public AuthorizationContext Context { get; }

    /// <summary>
    /// Callback to provide the authorization result.
    /// </summary>
    public TaskCompletionSource<AuthorizationResult> ResultCallback { get; }

    /// <summary>
    /// Initializes a new authorization required event.
    /// </summary>
    /// <param name="operation">The operation requiring authorization.</param>
    /// <param name="context">Authorization context.</param>
    public AuthorizationRequiredEventArgs(DangerousOperation operation, AuthorizationContext context)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ResultCallback = new TaskCompletionSource<AuthorizationResult>();
    }
}

/// <summary>
/// Result of safety system validation.
/// </summary>
public class SafetyValidationResult
{
    /// <summary>
    /// Whether the safety system is functioning correctly.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// List of safety system errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// List of safety system warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Initializes a new safety validation result.
    /// </summary>
    /// <param name="isValid">Whether the safety system is valid.</param>
    /// <param name="errors">List of errors.</param>
    /// <param name="warnings">List of warnings.</param>
    public SafetyValidationResult(bool isValid, IEnumerable<string> errors, IEnumerable<string> warnings)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
        Warnings = warnings.ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A validation result indicating success.</returns>
    public static SafetyValidationResult Success(IEnumerable<string>? warnings = null)
    {
        return new SafetyValidationResult(true, Array.Empty<string>(), warnings ?? Array.Empty<string>());
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">List of errors.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static SafetyValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
    {
        return new SafetyValidationResult(false, errors, warnings ?? Array.Empty<string>());
    }
}