using MacroStudio.Application.Services;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Infrastructure.Adapters;
using MacroStudio.Infrastructure.Storage;
using MacroStudio.Infrastructure.Utilities;
using MacroStudio.Infrastructure.Win32;
using Microsoft.Extensions.DependencyInjection;
using MacroStudio.Presentation.ViewModels;
using MacroStudio.Presentation.Services;

namespace MacroStudio.Presentation.Extensions;

/// <summary>
/// Extension methods for configuring services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services following hexagonal architecture principles.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddMacroStudioServices(this IServiceCollection services)
    {
        // Register presentation layer services
        services.AddPresentationServices();

        // Register application layer services
        services.AddApplicationServices();

        // Register infrastructure layer services (adapters)
        services.AddInfrastructureServices();

        return services;
    }

    /// <summary>
    /// Registers presentation layer services (ViewModels, Views).
    /// </summary>
    private static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        services.AddSingleton<LocalizationService>();

        // Register ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ScriptListViewModel>();
        services.AddSingleton<CommandGridViewModel>();
        services.AddSingleton<ExecutionControlViewModel>();
        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<LoggingViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<DebugViewModel>();

        // UI coordinators
        services.AddHostedService<SafetyUiCoordinator>();

        return services;
    }

    /// <summary>
    /// Registers application layer services (Use Cases, Application Services).
    /// </summary>
    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application services
        services.AddScoped<IScriptManager, ScriptManager>();
        services.AddScoped<IRecordingService, RecordingService>();
        services.AddSingleton<ISafetyService, SafetyService>();
        services.AddScoped<IExecutionService, ExecutionService>();
        services.AddScoped<LuaScriptRunner>();
        services.AddSingleton<ILoggingService, LoggingService>();

        // Register Arduino connection service
        services.AddSingleton<ArduinoConnectionService>();

        // Register factories for input simulators and hook services
        // Note: We need to register with specific implementations, so we use a factory function
        services.AddScoped<IInputSimulatorFactory>(sp =>
        {
            var software = sp.GetRequiredService<Win32InputSimulator>();
            var hardware = sp.GetRequiredService<ArduinoInputSimulator>();
            return new InputSimulatorFactory(software, hardware);
        });
        services.AddSingleton<IInputHookServiceFactory>(sp =>
        {
            var software = sp.GetRequiredService<Win32InputHookService>();
            var hardware = sp.GetRequiredService<ArduinoInputHookService>();
            return new InputHookServiceFactory(software, hardware);
        });

        return services;
    }

    /// <summary>
    /// Registers infrastructure layer services (Adapters implementing domain interfaces).
    /// </summary>
    private static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register input simulation services
        // Register IInputSimulator interface with Win32InputSimulator as default implementation
        // (for ViewModels that need basic input simulation, like CommandGridViewModel)
        services.AddScoped<IInputSimulator, Win32InputSimulator>();
        services.AddScoped<Win32InputSimulator>();
        services.AddScoped<ArduinoInputSimulator>();
        services.AddScoped<CoordinateTransformer>();
        services.AddScoped<TimingUtilities>();

        // Register input hook services for recording
        services.AddSingleton<Win32InputHookService>();
        services.AddSingleton<ArduinoInputHookService>();

        // Register Arduino services
        services.AddSingleton<IArduinoService, ArduinoSerialService>();

        // Register global hotkey service
        services.AddSingleton<IGlobalHotkeyService, Win32GlobalHotkeyService>();
        services.AddSingleton<IRecordingHotkeyHookService, Win32RecordingHotkeyHookService>();
        services.AddSingleton<IScriptHotkeyHookService, Win32ScriptHotkeyHookService>();

        // Register storage services
        services.AddScoped<IFileStorageService, JsonFileStorageService>();
        services.AddSingleton<IFileLogWriter, FileLogWriter>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();

        return services;
    }
}