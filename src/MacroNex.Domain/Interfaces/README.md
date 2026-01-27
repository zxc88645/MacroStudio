# Domain Service Interfaces

This directory contains the domain service interfaces that define the contracts for the core business operations of the MacroNex application. These interfaces follow the hexagonal architecture pattern, keeping the domain layer independent of infrastructure concerns.

## Core Service Interfaces

### IScriptManager
- **Purpose**: Manages automation scripts (CRUD operations, validation, search)
- **Key Methods**: 
  - `GetAllScriptsAsync()`, `GetScriptAsync(id)`, `CreateScriptAsync(name)`
  - `UpdateScriptAsync(script)`, `DeleteScriptAsync(id)`, `DuplicateScriptAsync(id)`
  - `ValidateScript(script)`, `SearchScriptsAsync(searchTerm)`
- **Validation**: Includes `ScriptValidationResult` for comprehensive script validation

### IRecordingService
- **Purpose**: Handles recording of user input and conversion to automation commands
- **Key Methods**:
  - `StartRecordingAsync(options)`, `StopRecordingAsync()`, `PauseRecordingAsync()`
  - `GetRecordedCommandsAsync()`, `ValidateRecordingSetupAsync()`
- **Features**: 
  - Recording sessions with state management (`RecordingSession`)
  - Configurable recording options (`RecordingOptions`)
  - Recording statistics and validation
- **Events**: `CommandRecorded`, `RecordingStateChanged`, `RecordingError`

### IExecutionService
- **Purpose**: Manages script execution with state control and safety features
- **Key Methods**:
  - `StartExecutionAsync(script, options)`, `PauseExecutionAsync()`, `ResumeExecutionAsync()`
  - `StopExecutionAsync()`, `StepExecutionAsync()`, `TerminateExecutionAsync()`
  - `ValidateScriptForExecutionAsync(script)`, `GetExecutionStatistics()`
- **Features**:
  - Execution sessions with progress tracking (`ExecutionSession`)
  - Configurable execution options (`ExecutionOptions`)
  - Execution statistics and time estimation
- **Events**: `ProgressChanged`, `StateChanged`, `ExecutionError`, `ExecutionCompleted`

## Supporting Service Interfaces

### IDomainValidationService
- **Purpose**: Centralized validation for domain entities and business rules
- **Key Methods**:
  - `ValidateScript(script)`, `ValidateCommand(command)`, `ValidateScriptName(name)`
  - `ValidateScriptSafety(script)`, `ValidateExecutionLimits(script, limits)`
  - `IdentifyDangerousOperations(script)`
- **Features**: Comprehensive validation with errors, warnings, and dangerous operation detection

### ISafetyService
- **Purpose**: Safety controls, kill switches, and dangerous operation authorization
- **Key Methods**:
  - `ActivateKillSwitchAsync(reason)`, `DeactivateKillSwitchAsync()`
  - `CheckExecutionLimits(script, limits)`, `RequestAuthorizationAsync(operation, context)`
  - `IsOperationAuthorized(operation)`, `ValidateSafetySystemAsync()`
- **Features**:
  - Kill switch management with events
  - Execution limits enforcement (`ExecutionLimits`, `LimitViolation`)
  - Dangerous operation authorization system
- **Events**: `KillSwitchActivated`, `AuthorizationRequired`

### IDomainEventHandler & IDomainEventPublisher
- **Purpose**: Domain event handling and publishing infrastructure
- **Features**:
  - Generic event handler interface (`IDomainEventHandler<TEventArgs>`)
  - Event publisher for decoupled communication
  - Specific handler interfaces for each event type

## Domain Events

All domain events inherit from `DomainEventArgs` and include:

### Recording Events
- `CommandRecordedEventArgs`: When a command is recorded
- `RecordingStateChangedEventArgs`: When recording state changes
- `RecordingErrorEventArgs`: When recording errors occur

### Execution Events
- `ExecutionProgressEventArgs`: Progress updates during execution
- `ExecutionStateChangedEventArgs`: When execution state changes
- `ExecutionErrorEventArgs`: When execution errors occur
- `ExecutionCompletedEventArgs`: When execution completes

### Safety Events
- `KillSwitchActivatedEventArgs`: When kill switch is activated
- `AuthorizationRequiredEventArgs`: When dangerous operation needs authorization

## Value Objects and Supporting Types

### Validation Types
- `DomainValidationResult`: Comprehensive validation results
- `ValidationError` & `ValidationWarning`: Detailed validation feedback
- `ValidationSeverity`: Success, Warning, Error levels

### Safety Types
- `ExecutionLimits`: Configurable safety limits
- `LimitViolation`: Specific limit violations
- `DangerousOperation`: Identified dangerous operations
- `AuthorizationResult`: Authorization decisions
- `AuthorizedOperation`: Authorized dangerous operations

### Session Types
- `RecordingSession`: Recording session state and data
- `ExecutionSession`: Execution session state and progress
- `RecordingOptions` & `ExecutionOptions`: Configuration options
- `RecordingStatistics` & `ExecutionStatistics`: Session metrics

## Architecture Benefits

1. **Separation of Concerns**: Domain logic is independent of infrastructure
2. **Testability**: Interfaces can be easily mocked for unit testing
3. **Flexibility**: Infrastructure implementations can be swapped without changing domain logic
4. **Event-Driven**: Decoupled communication through domain events
5. **Safety-First**: Comprehensive safety controls and validation built into the domain
6. **Comprehensive Validation**: Multi-level validation with detailed feedback

## Requirements Mapping

These interfaces fulfill the following requirements:
- **Requirements 1.1-1.6**: Script management through `IScriptManager`
- **Requirements 3.1-3.6**: Input recording through `IRecordingService`
- **Requirements 4.1-4.6**: Script execution through `IExecutionService`
- **Requirements 5.1-5.6**: Safety controls through `ISafetyService`
- **Requirements 9.1-9.6**: Architecture and testability through interface design