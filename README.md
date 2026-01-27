# MacroNex

A WPF desktop automation application built using .NET 10 with hexagonal architecture principles. The system enables users to record, edit, manage, and execute automation scripts while maintaining strict safety controls and comprehensive logging.

## Architecture

The application follows hexagonal (ports and adapters) architecture with clear separation of concerns:

### Project Structure

```
MacroNex/
?œâ??€ src/
??  ?œâ??€ MacroNex.Domain/           # Core business logic and entities
??  ??  ?œâ??€ Entities/                 # Domain entities (Script, Command hierarchy)
??  ??  ?œâ??€ ValueObjects/             # Value objects (Point, MouseButton, etc.)
??  ??  ?œâ??€ Interfaces/               # Domain interfaces (ports)
??  ??  ?”â??€ Events/                   # Domain events
??  ????  ?œâ??€ MacroNex.Application/      # Application services and use cases
??  ??  ?œâ??€ Services/                 # Application services
??  ??  ?œâ??€ UseCases/                 # Business workflows
??  ??  ?”â??€ DTOs/                     # Data transfer objects
??  ????  ?œâ??€ MacroNex.Infrastructure/   # External adapters
??  ??  ?œâ??€ Adapters/                 # Concrete implementations
??  ??  ?œâ??€ Win32/                    # Win32 API integration
??  ??  ?”â??€ Storage/                  # File system and JSON serialization
??  ????  ?”â??€ MacroNex.Presentation/     # WPF UI layer
??      ?œâ??€ ViewModels/               # MVVM ViewModels
??      ?œâ??€ Views/                    # WPF Views and UserControls
??      ?œâ??€ Converters/               # Value converters
??      ?”â??€ Extensions/               # Service registration extensions
???”â??€ tests/
    ?”â??€ MacroNex.Tests/            # Unit and property-based tests
```

### Technology Stack

- **.NET 10**: Target framework
- **WPF**: User interface framework
- **CommunityToolkit.Mvvm**: MVVM framework for data binding and commands
- **Microsoft.Extensions.DependencyInjection**: Dependency injection container
- **Microsoft.Extensions.Hosting**: Application hosting and lifecycle management
- **FsCheck.Xunit**: Property-based testing framework

### Key Features

- **Hexagonal Architecture**: Clean separation between domain logic and infrastructure
- **MVVM Pattern**: Proper separation of UI concerns using CommunityToolkit.Mvvm
- **Dependency Injection**: Configured using Microsoft.Extensions.DependencyInjection
- **Property-Based Testing**: Comprehensive testing using FsCheck.NET
- **Safety-First Design**: Emergency controls and execution limits
- **Comprehensive Logging**: Real-time monitoring and historical analysis

## Getting Started

### Prerequisites

- .NET 10 SDK
- Windows 10/11 (for WPF and Win32 API support)

### Building the Solution

#### Quick Development Build (Fastest)

For rapid development iterations, use the provided batch scripts:

```bash
# Fast Debug build (recommended for development)
build-dev.bat

# Build and run in Debug mode
run-dev.bat

# Release build (faster than publish, still optimized)
build-release.bat

# Full publish (slowest, for final distribution)
publish-macronex.bat
```

#### Manual Build Commands

```bash
# Clone the repository
git clone <repository-url>
cd MacroNex

# Restore dependencies and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project src/MacroNex.Presentation
```

#### Build Script Comparison

| Script | Mode | Speed | Use Case |
|--------|------|-------|----------|
| `build-dev.bat` | Debug | ??Fastest | Daily development |
| `run-dev.bat` | Debug | ??Fastest | Quick testing |
| `build-release.bat` | Release | ?¡âš¡ Fast | Testing optimized builds |
| `publish-macronex.bat` | Release + Package | ?¡âš¡??Slowest | Final distribution |

### Project Dependencies

The dependency flow follows hexagonal architecture principles:

```
Presentation ??Application ??Domain
Infrastructure ??Domain
Infrastructure ??Application (for concrete implementations)
Tests ??All layers (for comprehensive testing)
```

## Development Guidelines

### Adding New Features

1. **Domain First**: Start by defining domain entities and interfaces in the Domain layer
2. **Application Services**: Implement business workflows in the Application layer
3. **Infrastructure Adapters**: Create concrete implementations in the Infrastructure layer
4. **Presentation Layer**: Build ViewModels and Views using MVVM pattern
5. **Testing**: Write both unit tests and property-based tests

### Service Registration

Services are registered in `ServiceCollectionExtensions.cs` following the dependency injection pattern:

```csharp
services.AddMacroNexServices(); // Registers all layers
```

### Testing Strategy

- **Unit Tests**: Specific examples and edge cases
- **Property-Based Tests**: Universal properties across all valid inputs
- **Integration Tests**: End-to-end workflows

## Safety and Security

The application implements comprehensive safety mechanisms:

- **Kill Switch**: Emergency termination of all automation
- **Execution Limits**: Maximum command count and execution time
- **Authorization Prompts**: User consent for dangerous operations
- **Focus Warnings**: Countdown before execution starts

## Contributing

1. Follow hexagonal architecture principles
2. Maintain clear separation between layers
3. Write comprehensive tests for all new features
4. Use dependency injection for all external dependencies
5. Follow MVVM pattern in the presentation layer

## License

[License information to be added]