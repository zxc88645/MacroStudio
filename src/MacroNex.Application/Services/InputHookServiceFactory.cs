using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;

namespace MacroNex.Application.Services;

/// <summary>
/// Factory for creating IInputHookService instances based on InputMode.
/// </summary>
public interface IInputHookServiceFactory
{
    /// <summary>
    /// Gets the appropriate IInputHookService for the specified input mode.
    /// </summary>
    /// <param name="mode">The input mode.</param>
    /// <returns>The IInputHookService instance.</returns>
    IInputHookService GetInputHookService(InputMode mode);
}

/// <summary>
/// Factory implementation for creating IInputHookService instances.
/// </summary>
public sealed class InputHookServiceFactory : IInputHookServiceFactory
{
    private readonly IInputHookService _softwareInputHookService;
    private readonly IInputHookService _hardwareInputHookService;

    public InputHookServiceFactory(
        IInputHookService softwareInputHookService,
        IInputHookService hardwareInputHookService)
    {
        _softwareInputHookService = softwareInputHookService ?? throw new ArgumentNullException(nameof(softwareInputHookService));
        _hardwareInputHookService = hardwareInputHookService ?? throw new ArgumentNullException(nameof(hardwareInputHookService));
    }

    public IInputHookService GetInputHookService(InputMode mode)
    {
        return mode switch
        {
            InputMode.HighLevel => _softwareInputHookService,
            InputMode.LowLevel => _softwareInputHookService, // Same Win32 hook service for both high and low level
            InputMode.Hardware => _hardwareInputHookService,
            _ => throw new ArgumentException($"Unknown input mode: {mode}", nameof(mode))
        };
    }
}
