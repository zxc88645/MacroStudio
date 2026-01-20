using MacroStudio.Domain.Entities;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for validation rules and business logic.
/// Provides centralized validation for domain entities and operations.
/// </summary>
public interface IDomainValidationService
{
    /// <summary>
    /// Validates a script for general correctness and business rules.
    /// </summary>
    /// <param name="script">The script to validate.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    DomainValidationResult ValidateScript(Script script);

    /// <summary>
    /// Validates a command for correctness and safety.
    /// </summary>
    /// <param name="command">The command to validate.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    DomainValidationResult ValidateCommand(Command command);

    /// <summary>
    /// Validates a script name according to business rules.
    /// </summary>
    /// <param name="name">The script name to validate.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    DomainValidationResult ValidateScriptName(string name);

    /// <summary>
    /// Validates that a script is safe for execution.
    /// </summary>
    /// <param name="script">The script to validate for execution.</param>
    /// <returns>Validation result with safety warnings and dangerous operations.</returns>
    DomainValidationResult ValidateScriptSafety(Script script);

    /// <summary>
    /// Validates execution limits and constraints.
    /// </summary>
    /// <param name="script">The script to validate.</param>
    /// <param name="maxCommands">Maximum allowed commands.</param>
    /// <param name="maxExecutionTime">Maximum allowed execution time.</param>
    /// <returns>Validation result indicating if limits are exceeded.</returns>
    DomainValidationResult ValidateExecutionLimits(Script script, int maxCommands, TimeSpan maxExecutionTime);

    /// <summary>
    /// Identifies potentially dangerous operations in a script.
    /// </summary>
    /// <param name="script">The script to analyze.</param>
    /// <returns>List of dangerous operations found.</returns>
    IEnumerable<DangerousOperation> IdentifyDangerousOperations(Script script);

    /// <summary>
    /// Validates recording configuration and options.
    /// </summary>
    /// <param name="options">Recording options to validate.</param>
    /// <returns>Validation result with configuration errors.</returns>
    DomainValidationResult ValidateRecordingOptions(object options);

    /// <summary>
    /// Validates execution configuration and options.
    /// </summary>
    /// <param name="options">Execution options to validate.</param>
    /// <returns>Validation result with configuration errors.</returns>
    DomainValidationResult ValidateExecutionOptions(object options);
}

/// <summary>
/// Represents the result of domain validation.
/// </summary>
public class DomainValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// List of validation errors that prevent operation.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// List of validation warnings that don't prevent operation.
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings { get; }

    /// <summary>
    /// Overall severity of the validation result.
    /// </summary>
    public ValidationSeverity Severity { get; }

    /// <summary>
    /// Initializes a new domain validation result.
    /// </summary>
    /// <param name="isValid">Whether validation passed.</param>
    /// <param name="errors">List of validation errors.</param>
    /// <param name="warnings">List of validation warnings.</param>
    public DomainValidationResult(bool isValid, IEnumerable<ValidationError> errors, IEnumerable<ValidationWarning> warnings)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
        Warnings = warnings.ToList().AsReadOnly();
        
        if (Errors.Any())
            Severity = ValidationSeverity.Error;
        else if (Warnings.Any())
            Severity = ValidationSeverity.Warning;
        else
            Severity = ValidationSeverity.Success;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A validation result indicating success.</returns>
    public static DomainValidationResult Success(IEnumerable<ValidationWarning>? warnings = null)
    {
        return new DomainValidationResult(true, Array.Empty<ValidationError>(), warnings ?? Array.Empty<ValidationWarning>());
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static DomainValidationResult Failure(IEnumerable<ValidationError> errors, IEnumerable<ValidationWarning>? warnings = null)
    {
        return new DomainValidationResult(false, errors, warnings ?? Array.Empty<ValidationWarning>());
    }

    /// <summary>
    /// Creates a validation result with only warnings.
    /// </summary>
    /// <param name="warnings">List of validation warnings.</param>
    /// <returns>A validation result with warnings but no errors.</returns>
    public static DomainValidationResult WithWarnings(IEnumerable<ValidationWarning> warnings)
    {
        return new DomainValidationResult(true, Array.Empty<ValidationError>(), warnings);
    }
}

/// <summary>
/// Represents a validation error that prevents an operation.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Property or field that caused the error, if applicable.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// Additional context or details about the error.
    /// </summary>
    public object? Context { get; }

    /// <summary>
    /// Initializes a new validation error.
    /// </summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="propertyName">Property name, if applicable.</param>
    /// <param name="context">Additional context.</param>
    public ValidationError(string code, string message, string? propertyName = null, object? context = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        PropertyName = propertyName;
        Context = context;
    }

    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>
/// Represents a validation warning that doesn't prevent an operation.
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Warning code for programmatic handling.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable warning message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Property or field that caused the warning, if applicable.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// Additional context or details about the warning.
    /// </summary>
    public object? Context { get; }

    /// <summary>
    /// Initializes a new validation warning.
    /// </summary>
    /// <param name="code">Warning code.</param>
    /// <param name="message">Warning message.</param>
    /// <param name="propertyName">Property name, if applicable.</param>
    /// <param name="context">Additional context.</param>
    public ValidationWarning(string code, string message, string? propertyName = null, object? context = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        PropertyName = propertyName;
        Context = context;
    }

    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>
/// Represents the severity level of validation results.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Validation passed without issues.
    /// </summary>
    Success,

    /// <summary>
    /// Validation passed but with warnings.
    /// </summary>
    Warning,

    /// <summary>
    /// Validation failed with errors.
    /// </summary>
    Error
}

/// <summary>
/// Represents a potentially dangerous operation identified in a script.
/// </summary>
public class DangerousOperation
{
    /// <summary>
    /// Type of dangerous operation.
    /// </summary>
    public DangerousOperationType Type { get; }

    /// <summary>
    /// Description of the dangerous operation.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The command that represents the dangerous operation.
    /// </summary>
    public Command Command { get; }

    /// <summary>
    /// Index of the command in the script.
    /// </summary>
    public int CommandIndex { get; }

    /// <summary>
    /// Risk level of this operation.
    /// </summary>
    public RiskLevel RiskLevel { get; }

    /// <summary>
    /// Recommended action to mitigate the risk.
    /// </summary>
    public string RecommendedAction { get; }

    /// <summary>
    /// Initializes a new dangerous operation.
    /// </summary>
    /// <param name="type">Type of dangerous operation.</param>
    /// <param name="description">Description of the operation.</param>
    /// <param name="command">The command representing the operation.</param>
    /// <param name="commandIndex">Index of the command.</param>
    /// <param name="riskLevel">Risk level.</param>
    /// <param name="recommendedAction">Recommended action.</param>
    public DangerousOperation(DangerousOperationType type, string description, Command command, int commandIndex, RiskLevel riskLevel, string recommendedAction)
    {
        Type = type;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Command = command ?? throw new ArgumentNullException(nameof(command));
        CommandIndex = commandIndex;
        RiskLevel = riskLevel;
        RecommendedAction = recommendedAction ?? throw new ArgumentNullException(nameof(recommendedAction));
    }
}

/// <summary>
/// Types of dangerous operations that can be identified in scripts.
/// </summary>
public enum DangerousOperationType
{
    /// <summary>
    /// System key combinations that could cause system actions.
    /// </summary>
    SystemKeyCombo,

    /// <summary>
    /// Clicks on system UI elements.
    /// </summary>
    SystemUIClick,

    /// <summary>
    /// Very fast execution that could overwhelm the system.
    /// </summary>
    HighFrequencyExecution,

    /// <summary>
    /// Very long execution that could run indefinitely.
    /// </summary>
    LongRunningExecution,

    /// <summary>
    /// Large number of commands that could cause system stress.
    /// </summary>
    HighVolumeCommands,

    /// <summary>
    /// Potentially destructive key combinations.
    /// </summary>
    DestructiveKeyCombo,

    /// <summary>
    /// Clicks in dangerous screen areas.
    /// </summary>
    DangerousScreenArea
}

/// <summary>
/// Risk levels for dangerous operations.
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Low risk - minor inconvenience possible.
    /// </summary>
    Low,

    /// <summary>
    /// Medium risk - could cause application issues.
    /// </summary>
    Medium,

    /// <summary>
    /// High risk - could cause system issues.
    /// </summary>
    High,

    /// <summary>
    /// Critical risk - could cause data loss or system damage.
    /// </summary>
    Critical
}