using MacroStudio.Application.Services;
using MacroStudio.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MacroStudio.Tests.Application;

public class SafetyServiceTests
{
    [Fact]
    // Feature: macro-studio, Property 5: Safety Mechanism Effectiveness
    public async Task ActivateKillSwitchAsync_ShouldSetActiveAndRaiseEvent()
    {
        var service = new SafetyService(NullLogger<SafetyService>.Instance);

        var raised = false;
        service.KillSwitchActivated += (_, args) =>
        {
            raised = true;
            Assert.False(string.IsNullOrWhiteSpace(args.Reason));
        };

        await service.ActivateKillSwitchAsync("Test");

        Assert.True(service.IsKillSwitchActive);
        Assert.True(raised);
    }
}

