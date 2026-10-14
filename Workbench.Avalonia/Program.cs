using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Workbench.Avalonia;

public static class Program
{
    public const string DefaultBaseUrl = "http://localhost:5099";

    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UsePlatformDetect();

    public static string ReadBaseUrl()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
                return DefaultBaseUrl;
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("BaseUrl", out var url) && url.GetString() is { Length: > 0 } text)
                return text;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or IOException)
        {
        }

        return DefaultBaseUrl;
    }
}

public sealed class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(new ShellApi(Program.ReadBaseUrl()));
        base.OnFrameworkInitializationCompleted();
    }
}
