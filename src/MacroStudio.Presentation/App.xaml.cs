using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MacroStudio.Presentation.Extensions;
using MacroStudio.Domain.Interfaces;
using System.IO;
using System.Text;

namespace MacroStudio.Presentation;

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
                    "Macro Studio - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true;
                Shutdown(-1);
            };

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            WriteStartupLog("Startup failed", ex);
            MessageBox.Show(
                $"Macro Studio 啟動失敗：\n\n{ex.Message}\n\n已寫入記錄：{GetStartupLogPath()}",
                "Macro Studio - Startup Failed",
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
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register all MacroStudio services
                services.AddMacroStudioServices();
                
                // Register the main window
                services.AddSingleton<MainWindow>();
            });
    }

    private static string GetStartupLogPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacroStudio", "Logs");
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

