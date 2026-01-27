using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;

namespace MacroNex.Application.Services;

/// <summary>
/// Factory for creating IInputSimulator instances based on InputMode.
/// </summary>
public interface IInputSimulatorFactory
{
    /// <summary>
    /// Gets the appropriate IInputSimulator for the specified input mode.
    /// </summary>
    /// <param name="mode">The input mode.</param>
    /// <returns>The IInputSimulator instance.</returns>
    IInputSimulator GetInputSimulator(InputMode mode);
}

/// <summary>
/// Factory implementation for creating IInputSimulator instances.
/// </summary>
public sealed class InputSimulatorFactory : IInputSimulatorFactory
{
    private readonly IInputSimulator _softwareInputSimulator;
    private readonly IInputSimulator _hardwareInputSimulator;

    public InputSimulatorFactory(
        IInputSimulator softwareInputSimulator,
        IInputSimulator hardwareInputSimulator)
    {
        _softwareInputSimulator = softwareInputSimulator ?? throw new ArgumentNullException(nameof(softwareInputSimulator));
        _hardwareInputSimulator = hardwareInputSimulator ?? throw new ArgumentNullException(nameof(hardwareInputSimulator));
    }

    public IInputSimulator GetInputSimulator(InputMode mode)
    {
        return mode switch
        {
            InputMode.HighLevel => _softwareInputSimulator,
            InputMode.LowLevel => _softwareInputSimulator, // Same Win32 simulator, but will use low-level methods
            InputMode.Hardware => _hardwareInputSimulator,
            _ => throw new ArgumentException($"Unknown input mode: {mode}", nameof(mode))
        };
    }
}
