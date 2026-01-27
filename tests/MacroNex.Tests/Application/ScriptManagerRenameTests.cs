using MacroNex.Application.Services;
using MacroNex.Domain.Interfaces;
using MacroNex.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MacroNex.Tests.Application;

public class ScriptManagerRenameTests
{
    [Fact]
    public async Task RenameScriptAsync_RenamesAndPersists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHookService = new Mock<IScriptHotkeyHookService>();
            var storageService = new JsonFileStorageService(NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(storageService, mockHookService.Object, NullLogger<ScriptManager>.Instance);

            var script = await scriptManager.CreateScriptAsync("Old Name");

            await scriptManager.RenameScriptAsync(script.Id, "New Name");

            var loaded = await scriptManager.GetScriptAsync(script.Id);
            Assert.NotNull(loaded);
            Assert.Equal("New Name", loaded!.Name);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RenameScriptAsync_DuplicateName_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHookService = new Mock<IScriptHotkeyHookService>();
            var storageService = new JsonFileStorageService(NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(storageService, mockHookService.Object, NullLogger<ScriptManager>.Instance);

            var s1 = await scriptManager.CreateScriptAsync("Alpha");
            _ = await scriptManager.CreateScriptAsync("Bravo");

            await Assert.ThrowsAsync<InvalidOperationException>(() => scriptManager.RenameScriptAsync(s1.Id, "Bravo"));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}

