namespace RdpMan.Desktop;

internal sealed class PingForm : Form
{
    private readonly string _endpoint;
    private readonly Label _state = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label _statistics = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly ListBox _history = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly Button _toggle = AppTheme.Button("Stoppen");
    private CancellationTokenSource? _run;
    private int _sent;
    private int _received;

    public PingForm(MachineEntry machine)
    {
        _endpoint = machine.DnsName;
        Text = $"Ping – {machine.DisplayName}";
        Width = 780;
        Height = 460;
        MinimumSize = new Size(560, 320);
        AppTheme.ApplyWindow(this);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _state.Font = AppTheme.SectionFont;
        _state.Text = $"Prüfe {_endpoint} …";
        _statistics.Margin = new Padding(0, 8, 0, 12);
        _history.Font = new Font("Consolas", 10);
        _history.BackColor = AppTheme.Sidebar;
        _history.ForeColor = Color.FromArgb(187, 247, 208);
        _history.BorderStyle = BorderStyle.None;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 12, 0, 0) };
        var close = AppTheme.Button("Schließen");
        close.Click += (_, _) => Close();
        _toggle.Click += (_, _) => { if (_run is null) StartPinging(); else StopPinging(); };
        actions.Controls.Add(_toggle);
        actions.Controls.Add(close);
        root.Controls.Add(_state, 0, 0);
        root.Controls.Add(_statistics, 0, 1);
        root.Controls.Add(_history, 0, 2);
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); StartPinging(); }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel) StopPinging();
    }

    private void StopPinging()
    {
        _run?.Cancel();
        _run = null;
        _toggle.Text = "Starten";
        _state.Text = $"Pausiert – {_endpoint}";
        _state.ForeColor = AppTheme.MutedText;
    }

    private async void StartPinging()
    {
        if (_run is not null || IsDisposed) return;
        using var run = new CancellationTokenSource();
        _run = run;
        _toggle.Text = "Stoppen";
        _state.Text = $"Prüfe {_endpoint} …";
        _state.ForeColor = AppTheme.MutedText;
        try
        {
            while (!run.IsCancellationRequested)
            {
                var result = await PingProbe.CheckAsync(_endpoint, run.Token);
                if (run.IsCancellationRequested || IsDisposed) break;
                _sent++;
                if (result.Reachable) _received++;
                _state.Text = $"{(result.Reachable ? "Erreichbar" : "Keine Antwort")} – {_endpoint}";
                _state.ForeColor = result.Reachable ? AppTheme.Success : Color.Firebrick;
                _statistics.Text = $"Gesendet: {_sent}    Antworten: {_received}    Ohne Antwort: {_sent - _received}";
                if (_history.Items.Count >= 500) _history.Items.RemoveAt(0);
                _history.Items.Add($"{DateTime.Now:HH:mm:ss}  {result.Message}");
                _history.TopIndex = Math.Max(0, _history.Items.Count - 1);
                await Task.Delay(1000, run.Token);
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // Async UI event handlers must not let a probe failure terminate the app.
            if (!IsDisposed && !run.IsCancellationRequested)
            {
                _state.Text = $"Ping angehalten: {ex.Message}";
                _state.ForeColor = Color.Firebrick;
            }
        }
        finally
        {
            if (ReferenceEquals(_run, run))
            {
                _run = null;
                if (!IsDisposed) _toggle.Text = "Starten";
            }
        }
    }
}
