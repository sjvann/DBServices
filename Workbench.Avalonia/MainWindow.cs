using Avalonia.Controls;
using Avalonia.Layout;
using Workbench.Contracts;

namespace Workbench.Avalonia;

public sealed class MainWindow : Window
{
    private readonly ShellApi _api;
    private readonly StackPanel _main = new() { Spacing = 8 };
    private readonly TextBlock _status = new();
    private readonly TextBox _name = new() { PlaceholderText = "名稱" };
    private readonly TextBox _secret = new() { PlaceholderText = "秘密", PasswordChar = '*' };
    private readonly TextBox _utterance = new() { PlaceholderText = "語句" };
    private readonly TextBlock _entries = new();

    public MainWindow(ShellApi api)
    {
        _api = api;
        Title = "報表工作台";
        MinWidth = 720;
        MinHeight = 480;
        Width = 960;
        Height = 640;
        Content = Build();
    }

    private Control Build()
    {
        var login = new Button { Content = "登入" };
        login.Click += async (_, _) => await LoginAsync();
        var logout = new Button { Content = "登出" };
        logout.Click += async (_, _) => await LogoutAsync();
        var analyst = new Button { Content = "讀取檢視" };
        analyst.Click += async (_, _) => await ViewsAsync();
        var designer = new Button { Content = "讀取樣式" };
        designer.Click += async (_, _) => await StylesAsync();
        var terminal = new Button { Content = "送出語句" };
        terminal.Click += async (_, _) => await SubmitAsync();

        _main.Children.Add(new TextBlock { Text = "報表工作台", FontSize = 20 });
        _main.Children.Add(_name);
        _main.Children.Add(_secret);
        _main.Children.Add(login);
        _main.Children.Add(logout);
        _main.Children.Add(_entries);
        _main.Children.Add(analyst);
        _main.Children.Add(designer);
        _main.Children.Add(_utterance);
        _main.Children.Add(terminal);
        _main.Children.Add(_status);
        return new ScrollViewer { Content = _main };
    }

    private async Task LoginAsync()
    {
        var ok = await _api.LoginAsync(_name.Text ?? "", _secret.Text ?? "");
        if (!ok)
        {
            _status.Text = "登入失敗";
            return;
        }

        var me = await _api.MeAsync();
        _entries.Text = me is null ? "" : string.Join("、", me.Entries.Select(EntryNames.Label));
        _status.Text = me is null ? "" : me.Destination;
    }

    private async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        _entries.Text = "";
        _status.Text = "已登出";
    }

    private async Task ViewsAsync()
    {
        var views = await _api.ViewsAsync();
        _status.Text = views is null ? _api.LastError ?? "" : "檢視 " + views.Count;
    }

    private async Task StylesAsync()
    {
        var styles = await _api.StylesAsync();
        _status.Text = styles is null ? _api.LastError ?? "" : "樣式 " + styles.Count;
    }

    private async Task SubmitAsync()
    {
        var result = await _api.SubmitAsync(_utterance.Text ?? "");
        if (result is null)
        {
            _status.Text = _api.LastError ?? "";
            return;
        }

        _status.Text = result.Status == "ready"
            ? "列 " + result.Rows.Count
            : result.Reason;
    }
}
