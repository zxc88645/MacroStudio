using MacroNex.Application.Services;
using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MacroNex.Tests.Application;

public class LuaScriptRunnerTests
{
    [Fact]
    public async Task RunAsync_CallsHostApis()
    {
        var input = new Mock<IInputSimulator>(MockBehavior.Strict);
        var safety = new SafetyService(NullLogger<SafetyService>.Instance);

        input.Setup(i => i.SimulateMouseMoveAsync(It.IsAny<Point>())).Returns(Task.CompletedTask);
        input.Setup(i => i.SimulateMouseClickAsync(MouseButton.Left, ClickType.Click)).Returns(Task.CompletedTask);
        input.Setup(i => i.SimulateKeyboardInputAsync("hi")).Returns(Task.CompletedTask);
        input.Setup(i => i.SimulateKeyPressAsync(VirtualKey.VK_A, true)).Returns(Task.CompletedTask);
        input.Setup(i => i.SimulateKeyPressAsync(VirtualKey.VK_A, false)).Returns(Task.CompletedTask);

        var inputSimulatorFactory = new FakeInputSimulatorFactory(input.Object);
        var runner = new LuaScriptRunner(inputSimulatorFactory, safety, NullLogger<LuaScriptRunner>.Instance);

        var lua = @"
move(10, 20)
mouse_click('left')
type_text('hi')
key_down('a')
key_release('a')
";

        await runner.RunAsync(lua, CancellationToken.None, inputMode: InputMode.HighLevel);

        input.VerifyAll();
    }

    private sealed class FakeInputSimulatorFactory : IInputSimulatorFactory
    {
        private readonly IInputSimulator _inputSimulator;

        public FakeInputSimulatorFactory(IInputSimulator inputSimulator)
        {
            _inputSimulator = inputSimulator;
        }

        public IInputSimulator GetInputSimulator(InputMode mode)
        {
            return _inputSimulator;
        }
    }
}

