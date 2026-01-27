using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MacroNex.Presentation.Extensions;
using MacroNex.Domain.Interfaces;
using MacroNex.Infrastructure.Logging;
using MacroNex.Presentation.Services;
using System.IO;
using System.Text;

namespace MacroNex.Presentation;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            _host = CreateHostBuilder().Build();
            _host.Start();

            DispatcherUnhandledException += (sender, args) =>
            {
                try
                {
                    var logger = _host.Services.GetService<ILoggingService>();
                    logger?.LogErrorAsync("Unhandled UI exception", args.Exception).GetAwaiter().GetResult();
                }
                catch
                {
                }

                WriteStartupLog("Unhandled UI exception", args.Exception);
                MessageBox.Show(
                    $"發生未處理的 UI 例外：\n\n{args.Exception.Message}\n\n已寫入記錄：{GetStartupLogPath()}",
                    "MacroNex - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true;
                Shutdown(-1);
            };

            // Apply persisted UI language before showing any windows.
            try
            {
                var settingsService = _host.Services.GetRequiredService<ISettingsService>();
                var localization = _host.Services.GetRequiredService<LocalizationService>();
                // Avoid deadlock on UI thread: run async file I/O off the dispatcher thread.
                var settings = Task.Run(() => settingsService.LoadAsync()).GetAwaiter().GetResult();
                settings.EnsureDefaults();
                localization.ApplyLanguage(settings.UiLanguage);
            }
            catch
            {
                // Best-effort; fall back to default language dictionary.
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            WriteStartupLog("Startup failed", ex);
            MessageBox.Show(
                $"MacroNex 啟動失敗：\n\n{ex.Message}\n\n已寫入記錄：{GetStartupLogPath()}",
                "MacroNex - Startup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _host?.StopAsync().GetAwaiter().GetResult(); } catch { }
        _host?.Dispose();
        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacroNex",
            "Logs");
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, "diagnostic.log");

        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                // Keep default providers (Console, Debug) and add file logger
                // Console logger will show output in dotnet run console
                // Debug logger will show in Visual Studio output window
                // File logger will write to diagnostic.log file (only Warning and above)
                logging.AddProvider(new FileLoggerProvider(logFilePath));
            })
            .ConfigureServices((context, services) =>
            {
                // Register all MacroNex services
                services.AddMacroNexServices();

                // Register the main window
                services.AddSingleton<MainWindow>();
            });
    }

    private static string GetStartupLogPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacroNex", "Logs");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "startup.log");
    }

    private static void WriteStartupLog(string title, Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}");
            sb.AppendLine(ex.ToString());
            sb.AppendLine(new string('-', 80));
            File.AppendAllText(GetStartupLogPath(), sb.ToString());
        }
        catch
        {
        }
    }
}

