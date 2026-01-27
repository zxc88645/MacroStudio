# Implementation Plan: MacroNex

## Overview

This implementation plan breaks down the WPF MacroNex development into discrete, incremental tasks following hexagonal architecture principles. Each task builds upon previous work, ensuring continuous integration and early validation of core functionality. The plan emphasizes safety-first implementation with comprehensive testing at each stage.

## Tasks

- [x] 1. Project Setup and Core Architecture
  - Create .NET 8 WPF project with proper solution structure
  - Set up dependency injection container and MVVM framework (CommunityToolkit.Mvvm)
  - Configure project references and NuGet packages (FsCheck.NET for property testing)
  - Establish folder structure following hexagonal architecture (Domain, Application, Infrastructure, Presentation)
  - _Requirements: 9.4, 9.6_

- [ ] 2. Domain Layer Foundation
  - [x] 2.1 Implement core domain entities (Script, Command hierarchy)
    - Create Script entity with Id, Name, Commands, metadata properties
    - Implement Command abstract base class and concrete implementations (MouseMoveCommand, MouseClickCommand, KeyboardCommand, SleepCommand)
    - Add domain value objects (Point, MouseButton, VirtualKey, ExecutionState)
    - _Requirements: 1.1, 2.1, 3.1, 3.2_

  - [x] 2.2 Write property test for domain entity integrity
    - **Property 1: Script Lifecycle Integrity**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6**

  - [x] 2.3 Define domain service interfaces
    - Create IScriptManager, IRecordingService, IExecutionService interfaces
    - Define domain events and event handlers
    - Implement domain validation rules and business logic
    - _Requirements: 1.1, 3.1, 4.1_

- [ ] 3. Infrastructure Layer - Win32 Integration
  - [x] 3.1 Implement Win32 input simulation adapter
    - Create InputSimulator class implementing IInputSimulator interface
    - Implement SendInput wrapper for mouse and keyboard simulation
    - Add coordinate transformation and timing utilities
    - _Requirements: 3.1, 3.2, 4.1_

  - [ ] 3.2 Implement global hotkey service
    - Create GlobalHotkeyService using RegisterHotKey/UnregisterHotKey Win32 APIs
    - Implement hotkey registration, unregistration, and event handling
    - Add hotkey conflict detection and resolution
    - _Requirements: 3.3, 5.1, 5.5_

  - [~] 3.3 Write property test for input simulation accuracy
    - **Property 3: Recording Accuracy**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**

- [ ] 4. Infrastructure Layer - Storage and Serialization
  - [~] 4.1 Implement JSON storage service
    - Create FileStorageService implementing IFileStorageService interface
    - Implement JSON serialization/deserialization with versioned schema
    - Add schema migration support for backward compatibility
    - Handle file I/O operations with proper error handling
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [~] 4.2 Write property test for serialization round-trip
    - **Property 6: Serialization Round-Trip Consistency**
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5, 6.6**

- [ ] 5. Application Layer Services
  - [~] 5.1 Implement ScriptManager service
    - Create ScriptManager class with CRUD operations for scripts
    - Implement script validation, duplication, and metadata management
    - Add integration with storage service for persistence
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [~] 5.2 Implement RecordingService
    - Create RecordingService with Win32 hook integration (SetWindowsHookEx)
    - Implement event capture, filtering, and command generation
    - Add timing calculation and sleep command insertion
    - Handle recording state management and hotkey integration
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [~] 5.3 Write property test for command sequence preservation
    - **Property 2: Command Sequence Preservation**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**

- [~] 6. Checkpoint - Core Services Integration
  - Ensure all core services integrate properly with dependency injection
  - Verify domain services can be tested independently of infrastructure
  - Test basic script creation, recording, and storage workflows
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Application Layer - Execution Engine
  - [~] 7.1 Implement ExecutionService
    - Create ExecutionService with state management (start, pause, resume, stop, step)
    - Implement command execution loop with timing and speed control
    - Add execution limits enforcement and safety checks
    - Integrate with input simulation service for command playback
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [~] 7.2 Write property test for execution state management
    - **Property 4: Execution State Management**
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.6**

  - [~] 7.3 Implement safety and kill switch functionality
    - Add kill switch integration with global hotkey service
    - Implement execution limits (max commands, max time) with automatic termination
    - Create authorization prompt system for dangerous operations
    - Add focus warning with countdown before execution starts
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [~] 7.4 Write property test for safety mechanism effectiveness
    - **Property 5: Safety Mechanism Effectiveness**
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5, 5.6**

- [ ] 8. Application Layer - Logging System
  - [~] 8.1 Implement logging service
    - Create Logger service with multiple log levels (Info, Warning, Error)
    - Implement real-time logging with timestamps and context
    - Add persistent log file management and rotation
    - Create log filtering and search capabilities
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [~] 8.2 Write property test for logging completeness
    - **Property 7: Logging Completeness**
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**

- [ ] 9. Presentation Layer - MVVM ViewModels
  - [~] 9.1 Implement MainViewModel
    - Create MainViewModel with overall application state management
    - Implement global command handling and hotkey coordination
    - Add script collection management and selection handling
    - Integrate with all application services through dependency injection
    - _Requirements: 8.1, 8.2, 8.4_

  - [~] 9.2 Implement ScriptListViewModel
    - Create ScriptListViewModel with script collection display and management
    - Implement search and filtering functionality for scripts
    - Add script CRUD operation commands (create, delete, rename, copy)
    - Handle drag-and-drop operations for script organization
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 8.6_

  - [~] 9.3 Implement CommandGridViewModel
    - Create CommandGridViewModel for command sequence display and editing
    - Implement command manipulation (add, delete, reorder, modify parameters)
    - Add command validation and real-time feedback
    - Handle drag-and-drop for command reordering
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [ ] 10. Presentation Layer - Execution and Control ViewModels
  - [~] 10.1 Implement ExecutionControlViewModel
    - Create ExecutionControlViewModel with execution state management
    - Implement execution control commands (play, pause, resume, stop, step)
    - Add execution progress display and status indicators
    - Handle execution options and speed control settings
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 8.3_

  - [~] 10.2 Implement LoggingViewModel
    - Create LoggingViewModel for real-time log display
    - Implement log filtering, search, and export functionality
    - Add visual indicators for different log levels
    - Handle log persistence and historical analysis features
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [~] 10.3 Write property test for UI responsiveness
    - **Property 8: UI Responsiveness and Functionality**
    - **Validates: Requirements 8.2, 8.3, 8.4, 8.6**

- [ ] 11. WPF Views and User Interface
  - [~] 11.1 Create main window layout
    - Design MainWindow.xaml with proper layout structure
    - Implement left panel for script list with search/filter controls
    - Create center panel for command grid with editable fields
    - Add bottom panel for logging/console display
    - Add top-right area for execution controls and status
    - _Requirements: 8.1, 8.2_

  - [~] 11.2 Implement script list view
    - Create ScriptListView with data binding to ScriptListViewModel
    - Implement search and filter UI controls
    - Add context menu for script operations (create, delete, rename, copy)
    - Handle selection events and visual feedback
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 8.6_

  - [~] 11.3 Implement command grid view
    - Create CommandGridView with editable data grid for commands
    - Implement drag-and-drop for command reordering
    - Add command parameter editing with validation feedback
    - Create context menu for command operations (add, delete, modify)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [~] 12. Checkpoint - UI Integration Testing
  - Test complete UI workflows from script creation to execution
  - Verify data binding works correctly between ViewModels and Views
  - Test keyboard shortcuts and hotkey functionality
  - Ensure responsive UI performance with large script collections
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Safety and Security Implementation
  - [~] 13.1 Implement kill switch integration
    - Wire kill switch hotkey to execution termination
    - Add visual indicators for kill switch status
    - Test emergency termination across all execution scenarios
    - _Requirements: 5.1, 5.5_

  - [~] 13.2 Implement authorization and warning systems
    - Create authorization prompt dialogs for dangerous operations
    - Implement focus warning with 3-second countdown
    - Add first-time operation tracking and consent management
    - _Requirements: 5.2, 5.3_

  - [~] 13.3 Implement execution limits and monitoring
    - Add execution limit configuration and enforcement
    - Implement automatic termination when limits exceeded
    - Create monitoring and alerting for safety violations
    - _Requirements: 5.4, 5.6_

- [ ] 14. Import/Export and Configuration
  - [~] 14.1 Implement script import/export functionality
    - Create file dialogs for import/export operations
    - Add JSON validation and error handling for imports
    - Implement batch operations for multiple script management
    - _Requirements: 6.3, 6.4, 6.6_

  - [~] 14.2 Implement application configuration system
    - Create settings management for hotkeys, limits, and preferences
    - Add configuration persistence and loading
    - Implement configuration validation and migration
    - _Requirements: 5.5, 8.4_

- [ ] 15. Final Integration and Polish
  - [~] 15.1 Complete application wiring and startup
    - Wire all components together in App.xaml.cs with dependency injection
    - Implement application startup sequence and initialization
    - Add proper resource management and cleanup on shutdown
    - Handle application-level exception management
    - _Requirements: 8.1, 9.4, 9.6_

  - [~] 15.2 Write integration tests for complete workflows
    - Test end-to-end scenarios from recording to execution
    - Verify safety mechanisms work in real-world scenarios
    - Test error recovery and graceful degradation
    - _Requirements: All requirements_

- [~] 16. Final Checkpoint - Complete System Validation
  - Run full test suite including unit tests and property-based tests
  - Perform manual testing of all major workflows
  - Verify safety mechanisms and kill switch functionality
  - Test import/export and configuration management
  - Validate performance with large scripts and extended execution
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with comprehensive testing ensure robust implementation from the start
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation and early issue detection
- Property tests validate universal correctness properties across all inputs
- Unit tests validate specific examples, edge cases, and integration points
- Safety implementation is prioritized throughout to ensure user control and system protection