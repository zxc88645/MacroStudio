using MacroNex.Application.Services;
using MacroNex.Domain.Entities;
using MacroNex.Domain.ValueObjects;
using Xunit;

namespace MacroNex.Tests.Application;

public class ScriptTextConverterKeyPressTests
{
    [Fact]
    public void Parse_KeyDown_ProducesKeyPressCommandDown()
    {
        var cmds = ScriptTextConverter.Parse("key_down('a')");
        Assert.Single(cmds);
        var kp = Assert.IsType<KeyPressCommand>(cmds[0]);
        Assert.Equal(VirtualKey.VK_A, kp.Key);
        Assert.True(kp.IsDown);
    }

    [Fact]
    public void Parse_KeyRelease_ProducesKeyPressCommandUp()
    {
        var cmds = ScriptTextConverter.Parse("key_release('a')");
        Assert.Single(cmds);
        var kp = Assert.IsType<KeyPressCommand>(cmds[0]);
        Assert.Equal(VirtualKey.VK_A, kp.Key);
        Assert.False(kp.IsDown);
    }

    [Fact]
    public void ToText_KeyPressCommand_EmitsKeyDownOrRelease()
    {
        var script = new Script("s");
        script.AddCommand(new KeyPressCommand(VirtualKey.VK_A, isDown: true));
        script.AddCommand(new KeyPressCommand(VirtualKey.VK_A, isDown: false));

        var text = ScriptTextConverter.ToText(script);
        Assert.Contains("key_down('a')", text);
        Assert.Contains("key_release('a')", text);
    }
}

