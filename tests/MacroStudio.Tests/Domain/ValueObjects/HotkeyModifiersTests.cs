using MacroStudio.Domain.ValueObjects;
using Xunit;

namespace MacroStudio.Tests.Domain.ValueObjects;

/// <summary>
/// Unit tests for HotkeyModifiers enum and its extension methods.
/// </summary>
public class HotkeyModifiersTests
{
    [Theory]
    [InlineData(HotkeyModifiers.None, 0u)]
    [InlineData(HotkeyModifiers.Alt, 0x0001u)]
    [InlineData(HotkeyModifiers.Control, 0x0002u)]
    [InlineData(HotkeyModifiers.Shift, 0x0004u)]
    [InlineData(HotkeyModifiers.Windows, 0x0008u)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x0003u)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x0006u)]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x0005u)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x0007u)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Windows, 0x000Au)]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Windows, 0x0009u)]
    [InlineData(HotkeyModifiers.Shift | HotkeyModifiers.Windows, 0x000Cu)]
    public void ToWin32Modifiers_WithVariousModifiers_ShouldReturnCorrectFlags(HotkeyModifiers modifiers, uint expectedFlags)
    {
        // Act
        var win32Modifiers = modifiers.ToWin32Modifiers();

        // Assert
        Assert.Equal(expectedFlags, win32Modifiers);
    }

    [Theory]
    [InlineData(0u, HotkeyModifiers.None)]
    [InlineData(0x0001u, HotkeyModifiers.Alt)]
    [InlineData(0x0002u, HotkeyModifiers.Control)]
    [InlineData(0x0004u, HotkeyModifiers.Shift)]
    [InlineData(0x0008u, HotkeyModifiers.Windows)]
    [InlineData(0x0003u, HotkeyModifiers.Control | HotkeyModifiers.Alt)]
    [InlineData(0x0006u, HotkeyModifiers.Control | HotkeyModifiers.Shift)]
    [InlineData(0x0005u, HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    [InlineData(0x0007u, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    [InlineData(0x000Au, HotkeyModifiers.Control | HotkeyModifiers.Windows)]
    [InlineData(0x0009u, HotkeyModifiers.Alt | HotkeyModifiers.Windows)]
    [InlineData(0x000Cu, HotkeyModifiers.Shift | HotkeyModifiers.Windows)]
    public void FromWin32Modifiers_WithVariousFlags_ShouldReturnCorrectModifiers(uint win32Flags, HotkeyModifiers expectedModifiers)
    {
        // Act
        var modifiers = HotkeyModifiersExtensions.FromWin32Modifiers(win32Flags);

        // Assert
        Assert.Equal(expectedModifiers, modifiers);
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, "None")]
    [InlineData(HotkeyModifiers.Control, "Ctrl")]
    [InlineData(HotkeyModifiers.Alt, "Alt")]
    [InlineData(HotkeyModifiers.Shift, "Shift")]
    [InlineData(HotkeyModifiers.Windows, "Win")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, "Ctrl + Alt")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift, "Ctrl + Shift")]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Shift, "Alt + Shift")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, "Ctrl + Alt + Shift")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Windows, "Ctrl + Win")]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Windows, "Alt + Win")]
    [InlineData(HotkeyModifiers.Shift | HotkeyModifiers.Windows, "Shift + Win")]
    public void GetDisplayString_WithVariousModifiers_ShouldReturnCorrectString(HotkeyModifiers modifiers, string expected)
    {
        // Act
        var displayString = modifiers.GetDisplayString();

        // Assert
        Assert.Equal(expected, displayString);
    }

    [Theory]
    [InlineData(HotkeyModifiers.None)]
    [InlineData(HotkeyModifiers.Control)]
    [InlineData(HotkeyModifiers.Alt)]
    [InlineData(HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Windows)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Windows)]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Windows)]
    [InlineData(HotkeyModifiers.Shift | HotkeyModifiers.Windows)]
    public void IsValidCombination_WithAllModifiers_ShouldReturnTrue(HotkeyModifiers modifiers)
    {
        // Act
        var isValid = modifiers.IsValidCombination();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void FlagsEnum_ShouldSupportBitwiseOperations()
    {
        // Arrange
        var control = HotkeyModifiers.Control;
        var alt = HotkeyModifiers.Alt;
        var shift = HotkeyModifiers.Shift;

        // Act
        var combined = control | alt | shift;
        var hasControl = combined.HasFlag(HotkeyModifiers.Control);
        var hasAlt = combined.HasFlag(HotkeyModifiers.Alt);
        var hasShift = combined.HasFlag(HotkeyModifiers.Shift);
        var hasWindows = combined.HasFlag(HotkeyModifiers.Windows);

        // Assert
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, combined);
        Assert.True(hasControl);
        Assert.True(hasAlt);
        Assert.True(hasShift);
        Assert.False(hasWindows);
    }

    [Fact]
    public void CommonCombinations_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyModifiers.ControlAlt);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, HotkeyModifiers.ControlShift);
        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Shift, HotkeyModifiers.AltShift);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, HotkeyModifiers.ControlAltShift);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Windows, HotkeyModifiers.ControlWindows);
        Assert.Equal(HotkeyModifiers.Alt | HotkeyModifiers.Windows, HotkeyModifiers.AltWindows);
        Assert.Equal(HotkeyModifiers.Shift | HotkeyModifiers.Windows, HotkeyModifiers.ShiftWindows);
    }

    [Fact]
    public void RoundTripConversion_ShouldPreserveValues()
    {
        // Arrange
        var originalModifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;

        // Act
        var win32Flags = originalModifiers.ToWin32Modifiers();
        var convertedBack = HotkeyModifiersExtensions.FromWin32Modifiers(win32Flags);

        // Assert
        Assert.Equal(originalModifiers, convertedBack);
    }
}