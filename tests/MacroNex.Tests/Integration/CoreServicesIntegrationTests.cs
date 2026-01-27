using MacroNex.Application.Services;
using MacroNex.Domain.Entities;
using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using MacroNex.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MacroNex.Tests.Integration;

public class CoreServicesIntegrationTests
{
    [Fact]
    public async Task ScriptManager_CreateUpdateDelete_RoundTripThroughStorage_Works()
    {
        // Arrange: DI container with real services but isolated storage directory
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", "integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var services = new ServiceCollection();

            services.AddSingleton<Microsoft.Extensions.Logging.ILogger<JsonFileStorageService>>(NullLogger<JsonFileStorageService>.Instance);
            services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ScriptManager>>(NullLogger<ScriptManager>.Instance);

            services.AddSingleton<IFileStorageService>(sp =>
                new JsonFileStorageService(sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JsonFileStorageService>>(), tempDir));

            services.AddSingleton<IScriptHotkeyHookService>(sp => new FakeScriptHotkeyHookService());
            services.AddSingleton<IScriptManager>(sp =>
                new ScriptManager(sp.GetRequiredService<IFileStorageService>(), sp.GetRequiredService<IScriptHotkeyHookService>(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScriptManager>>()));

            var provider = services.BuildServiceProvider();
            var scriptManager = provider.GetRequiredService<IScriptManager>();

            // Act: create
            var script = await scriptManager.CreateScriptAsync("Integration Script");
            Assert.NotNull(script);

            // Act: update (add a command)
            script.AddCommand(new MouseMoveCommand(new Point(10, 20)) { Delay = TimeSpan.FromMilliseconds(25) });
            await scriptManager.UpdateScriptAsync(script);

            // Assert: reload and verify persisted
            var loaded = await scriptManager.GetScriptAsync(script.Id);
            Assert.NotNull(loaded);
            Assert.Equal(script.Id, loaded!.Id);
            Assert.Equal("Integration Script", loaded.Name);
            Assert.Equal(1, loaded.CommandCount);

            // Act: delete
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);
            Assert.True(deleted);

            // Assert: gone
            var afterDelete = await scriptManager.GetScriptAsync(script.Id);
            Assert.Null(afterDelete);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private sealed class FakeScriptHotkeyHookService : IScriptHotkeyHookService
    {
#pragma warning disable CS0067 // Event is part of interface contract; not used in this test double.
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
#pragma warning restore CS0067
        public void SetScriptHotkeys(IReadOnlyDictionary<Guid, HotkeyDefinition> hotkeys) { }
    }
}

