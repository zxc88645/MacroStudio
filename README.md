# MacroStudio

A WPF desktop automation application built using .NET 8 with hexagonal architecture principles. The system enables users to record, edit, manage, and execute automation scripts while maintaining strict safety controls and comprehensive logging.

## Architecture

The application follows hexagonal (ports and adapters) architecture with clear separation of concerns:

### Project Structure

```
MacroStudio/
├── src/
│   ├── MacroStudio.Domain/           # Core business logic and entities
│   │   ├── Entities/                 # Domain entities (Script, Command hierarchy)
│   │   ├── ValueObjects/             # Value objects (Point, MouseButton, etc.)
│   │   ├── Interfaces/               # Domain interfaces (ports)
│   │   └── Events/                   # Domain events
│   │
│   ├── MacroStudio.Application/      # Application services and use cases
│   │   ├── Services/                 # Application services
│   │   ├── UseCases/                 # Business workflows
│   │   └── DTOs/                     # Data transfer objects
│   │
│   ├── MacroStudio.Infrastructure/   # External adapters
│   │   ├── Adapters/                 # Concrete implementations
│   │   ├── Win32/                    # Win32 API integration
│   │   └── Storage/                  # File system and JSON serialization
│   │
│   └── MacroStudio.Presentation/     # WPF UI layer
│       ├── ViewModels/               # MVVM ViewModels
│       ├── Views/                    # WPF Views and UserControls
│       ├── Converters/               # Value converters
│       └── Extensions/               # Service registration extensions
│
└── tests/
    └── MacroStudio.Tests/            # Unit and property-based tests
```

### Technology Stack

- **.NET 8**: Target framework
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

- .NET 8 SDK
- Windows 10/11 (for WPF and Win32 API support)

### Building the Solution

```bash
# Clone the repository
git clone <repository-url>
cd MacroStudio

# Restore dependencies and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project src/MacroStudio.Presentation
```

### Project Dependencies

The dependency flow follows hexagonal architecture principles:

```
Presentation → Application → Domain
Infrastructure → Domain
Infrastructure → Application (for concrete implementations)
Tests → All layers (for comprehensive testing)
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
services.AddMacroStudioServices(); // Registers all layers
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