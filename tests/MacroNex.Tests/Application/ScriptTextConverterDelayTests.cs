using MacroNex.Application.Services;
using MacroNex.Domain.Entities;
using MacroNex.Domain.ValueObjects;
using Xunit;

namespace MacroNex.Tests.Application;

public class ScriptTextConverterDelayTests
{
    [Fact]
    public void ToText_WhenCommandHasDelay_EmitsMsleepBeforeCommand()
    {
        var script = new Script("s");
        script.AddCommand(new KeyPressCommand(VirtualKey.VK_A, isDown: true)
        {
            Delay = TimeSpan.FromMilliseconds(123)
        });

        var text = ScriptTextConverter.ToText(script);

        var idxSleep = text.IndexOf("msleep(123)", StringComparison.Ordinal);
        var idxKey = text.IndexOf("key_down('a')", StringComparison.Ordinal);

        Assert.True(idxSleep >= 0, "Expected msleep(...) to be emitted.");
        Assert.True(idxKey >= 0, "Expected key_down(...) to be emitted.");
        Assert.True(idxSleep < idxKey, "Expected msleep(...) to appear before the command.");
    }

    [Fact]
    public void ToText_WhenDelayIsZero_DoesNotEmitMsleep()
    {
        var script = new Script("s");
        script.AddCommand(new KeyPressCommand(VirtualKey.VK_A, isDown: true)
        {
            Delay = TimeSpan.Zero
        });

        var text = ScriptTextConverter.ToText(script);

        Assert.DoesNotContain("msleep(", text, StringComparison.Ordinal);
        Assert.Contains("key_down('a')", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToText_WhenDelayHasFractionalMilliseconds_RoundsToIntegerMs()
    {
        var script = new Script("s");
        script.AddCommand(new KeyPressCommand(VirtualKey.VK_A, isDown: true)
        {
            Delay = TimeSpan.FromMilliseconds(123.6)
        });

        var text = ScriptTextConverter.ToText(script);

        Assert.Contains("msleep(124)", text, StringComparison.Ordinal);
    }
}

