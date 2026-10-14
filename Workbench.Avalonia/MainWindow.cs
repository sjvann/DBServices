using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Workbench.Contracts;

namespace Workbench.Avalonia;

public sealed class MainWindow : Window
{
    private readonly ShellApi _api;
    private readonly StackPanel _main = new() { Spacing = 8 };
    private readonly TextBlock _status = new() { Foreground = new SolidColorBrush(Color.Parse(DesignTokens.InkPrimary)), FontSize = 14 };
    private readonly ComboBox _connections = new();
    private readonly ComboBox _tables = new();
    private readonly TextBox _filterKey = new();
    private readonly TextBox _filterValue = new();
    private readonly DataGridLike _grid = new();
    private readonly TextBox _name = new();
    private readonly TextBox _secret = new();
    private readonly TextBox _phrase = new();
    private readonly ComboBox _operators = new();
    private string? _snapshotId;
    private List<List<string>> _rows = [];
    private List<string> _columns = [];

    public MainWindow(ShellApi api)
    {
        _api = api;
        Title = "報表工作台";
        MinWidth = 1100;
        MinHeight = 720;
        Width = 1280;
        Height = 800;
        Background = new SolidColorBrush(Color.Parse(DesignTokens.SurfaceBase));
        Content = Build();
    }

    public bool QueryEnabled => _tables.SelectedItem is string { Length: > 0 };
    public bool CountEnabled => QueryEnabled;

    private Control Build()
    {
        var query = new Button { Content = "查詢", Background = new SolidColorBrush(Color.Parse(DesignTokens.Action)), Foreground = Brushes.White };
        query.Click += async (_, _) => await QueryAsync();
        var count = new Button { Content = "計數", Background = new SolidColorBrush(Color.Parse(DesignTokens.Action)), Foreground = Brushes.White };
        count.Click += async (_, _) => await CountAsync();
        _tables.SelectionChanged += (_, _) =>
        {
            query.IsEnabled = QueryEnabled;
            count.IsEnabled = CountEnabled;
        };
        query.IsEnabled = false;
        count.IsEnabled = false;

        var login = new Button { Content = "登入" };
        login.Click += async (_, _) => await LoginAsync();
        var logout = new Button { Content = "登出" };
        logout.Click += async (_, _) => await _api.LogoutAsync();
        var load = new Button { Content = "載入資料表" };
        load.Click += async (_, _) => await LoadTablesAsync();
        var saveRoles = new Button { Content = "儲存角色" };
        saveRoles.Click += async (_, _) => await SaveRolesAsync();
        var openFavorite = new Button { Content = "開啟常用" };
        openFavorite.Click += async (_, _) => await OpenFavoriteAsync();
        var pdf = new Button { Content = "匯出 PDF" };
        pdf.Click += async (_, _) => await ExportAsync("pdf");
        var csv = new Button { Content = "匯出 CSV" };
        csv.Click += async (_, _) => await ExportAsync("csv");
        var print = new Button { Content = "列印" };
        print.Click += async (_, _) => await ExportAsync("print");
        var keyword = new Button { Content = "加入關鍵字" };
        keyword.Click += async (_, _) => await AddKeywordAsync();

        _main.Children.Add(new TextBlock { Text = "報表工作台", FontSize = 20, Foreground = new SolidColorBrush(Color.Parse(DesignTokens.InkPrimary)) });
        _main.Children.Add(_name);
        _main.Children.Add(_secret);
        _main.Children.Add(login);
        _main.Children.Add(logout);
        _main.Children.Add(_connections);
        _main.Children.Add(load);
        _main.Children.Add(_tables);
        _main.Children.Add(_filterKey);
        _main.Children.Add(_filterValue);
        _main.Children.Add(query);
        _main.Children.Add(count);
        _main.Children.Add(_grid);
        _main.Children.Add(_operators);
        _main.Children.Add(saveRoles);
        _main.Children.Add(openFavorite);
        _main.Children.Add(pdf);
        _main.Children.Add(csv);
        _main.Children.Add(print);
        _main.Children.Add(_phrase);
        _main.Children.Add(keyword);
        _main.Children.Add(_status);
        return new ScrollViewer { Content = _main };
    }

    public async Task LoginAsync()
    {
        var ok = await _api.LoginAsync(_name.Text ?? "", _secret.Text ?? "");
        _status.Text = ok ? "已登入" : (_api.LastError ?? "登入失敗");
        if (ok)
            await LoadConnectionsAsync();
    }

    public async Task LoadConnectionsAsync()
    {
        var connections = await _api.ConnectionsAsync(SurfaceNames.Mcp);
        _connections.ItemsSource = connections.Select(item => item.Name).ToList();
        if (connections.Count > 0)
            _connections.SelectedIndex = 0;
    }

    public async Task LoadTablesAsync()
    {
        if (_connections.SelectedItem is not string connection)
            return;
        var tables = await _api.TablesAsync(SurfaceNames.Mcp, connection);
        _tables.ItemsSource = tables.Tables;
    }

    public async Task QueryAsync()
    {
        if (!QueryEnabled)
            return;
        var response = await _api.QueryAsync(Request());
        Apply(response, keepRows: true);
    }

    public async Task CountAsync()
    {
        if (!CountEnabled)
            return;
        var response = await _api.CountAsync(Request());
        if (!string.IsNullOrEmpty(response.ErrorCode))
        {
            _status.Text = response.ErrorCode;
            return;
        }

        _status.Text = (response.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _snapshotId = response.SnapshotId ?? _snapshotId;
    }

    public async Task SaveRolesAsync()
    {
        if (_operators.SelectedItem is not string operatorId)
            return;
        var current = await _api.GetAssignmentsAsync(operatorId);
        var groups = await _api.GroupsAsync();
        if (current.Groups.Count == 0)
            current.Groups = groups.Select(item => new GroupRoleDto { GroupId = item.Id, Role = null }).ToList();
        await _api.SaveAssignmentsAsync(operatorId, current);
    }

    public async Task OpenFavoriteAsync()
    {
        var opened = await _api.OpenFavoriteAsync("current");
        _status.Text = opened.Difference ?? opened.Template?.Name ?? "";
    }

    public async Task ExportAsync(string kind)
    {
        if (string.IsNullOrEmpty(_snapshotId))
            return;
        if (kind == "pdf")
            await _api.PdfAsync(_snapshotId);
        else if (kind == "csv")
            _status.Text = await _api.CsvAsync(_snapshotId);
        else
            _status.Text = await _api.PrintAsync(_snapshotId);
    }

    public async Task AddKeywordAsync()
    {
        if (string.IsNullOrEmpty(_snapshotId))
            return;
        var ok = await _api.AddKeywordAsync(new KeywordWriteRequest { Phrase = _phrase.Text, SnapshotId = _snapshotId });
        _status.Text = ok ? "已加入關鍵字" : "無法加入關鍵字";
    }

    public void ShowOperators(IReadOnlyList<string> ids)
    {
        var values = ids.ToList();
        _operators.ItemsSource = values;
        if (values.Count > 0)
            _operators.SelectedIndex = 0;
    }

    private PlanRequest Request() => new()
    {
        Surface = SurfaceNames.Mcp,
        Connection = _connections.SelectedItem as string ?? "",
        Table = _tables.SelectedItem as string ?? "",
        Filters = string.IsNullOrWhiteSpace(_filterKey.Text) ? [] : [new FilterDto { Key = _filterKey.Text, Value = _filterValue.Text }],
    };

    private void Apply(PlanResponse response, bool keepRows)
    {
        if (!string.IsNullOrEmpty(response.ErrorCode))
        {
            _status.Text = response.ErrorCode + " " + response.ErrorMessage;
            return;
        }

        _columns = response.Columns;
        _rows = response.Rows;
        _snapshotId = response.SnapshotId;
        _grid.Show(_columns, _rows);
        _status.Text = response.Notice ?? "";
    }
}

public sealed class DataGridLike : StackPanel
{
    public void Show(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Children.Clear();
        Children.Add(new TextBlock { Text = string.Join(" | ", columns), FontFamily = new FontFamily("Cascadia Mono, Consolas") });
        foreach (var row in rows)
            Children.Add(new TextBlock { Text = string.Join(" | ", row) });
    }
}
