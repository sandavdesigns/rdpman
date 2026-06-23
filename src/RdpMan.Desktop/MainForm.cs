using System.Diagnostics;
using System.Reflection;

namespace RdpMan.Desktop;

public sealed class MainForm : Form
{
    private static readonly TimeSpan ConnectionStartupGrace = TimeSpan.FromMinutes(1);

    private readonly DataStore _store = new();
    private readonly Dictionary<Guid, IRemoteSessionHost> _sessions = [];
    private readonly Dictionary<Guid, DateTime> _sessionStartedAt = [];
    private readonly Dictionary<Guid, bool> _sessionShowErrors = [];
    private readonly HashSet<Guid> _sessionWasConnected = [];
    private readonly List<MachineEntry> _temporaryMachines = [];
    private readonly System.Windows.Forms.Timer _sessionSweepTimer = new();
    private bool _restoredRememberedSessions;
    private bool _syncingMachineSelection;
    private Guid? _toolTipMachineId;
    private Guid? _selectedMachineId;
    private AppData _data = new();

    private readonly ListBox _connectedMachineList = new();
    private readonly ListBox _machineList = new();
    private readonly Panel _machineListHost = new();
    private readonly Label _connectedHeader = new();
    private readonly TextBox _search = AppTheme.TextBox();
    private readonly ToolTip _machineToolTip = new();
    private readonly Label _machineCount = new();
    private readonly Panel _rdpPanel = new();
    private readonly Label _placeholder = new();
    private readonly ContextMenuStrip _machineMenu = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripStatusLabel _versionLabel = new();
    private int _logoClickCount;
    private DateTime _lastLogoClickAt;

    public MainForm()
    {
        Text = Brand.AppName;
        MinimumSize = new Size(1120, 720);
        Width = 1340;
        Height = 840;
        AppTheme.ApplyWindow(this);

        BuildLayout();
        LoadData();
        RefreshMachineList();

        _sessionSweepTimer.Interval = 1500;
        _sessionSweepTimer.Tick += (_, _) => SweepDisconnectedSessions();
        _sessionSweepTimer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        OpenSetupWizardIfNeeded();
        RestoreRememberedSessions();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _sessionSweepTimer.Stop();
        RememberConnectedSessions();
        SaveData();
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = AppTheme.Window,
            Padding = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var sidebar = BuildSidebar();
        var workspace = BuildWorkspace();

        root.Controls.Add(sidebar, 0, 0);
        root.Controls.Add(workspace, 1, 0);

        _statusStrip.BackColor = Color.White;
        _statusStrip.SizingGrip = true;
        _statusStrip.Items.Add(_statusLabel);
        _statusStrip.Items.Add(_versionLabel);
        _statusLabel.Text = "Bereit";
        _statusLabel.Spring = true;
        _versionLabel.Text = $"v{AppVersion()}";
        _versionLabel.ForeColor = AppTheme.MutedText;
        _statusLabel.ForeColor = AppTheme.MutedText;

        Controls.Add(root);
        Controls.Add(_statusStrip);
        _statusStrip.Dock = DockStyle.Bottom;
    }

    private static string AppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Sidebar,
            Padding = new Padding(18),
        };

        var header = new Panel { Dock = DockStyle.Top, Height = 94, BackColor = AppTheme.Sidebar };
        header.Paint += (_, e) => AppTheme.DrawLogo(e.Graphics, new Rectangle(0, 6, 56, 56));
        header.MouseClick += HeaderMouseClick;

        var title = new Label
        {
            Text = Brand.AppName,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
            Location = new Point(70, 10),
            AutoSize = true,
        };
        var subtitle = new Label
        {
            Text = Brand.AppSubtitle,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = AppTheme.SmallFont,
            Location = new Point(72, 42),
            AutoSize = true,
        };
        _machineCount.Text = "0 Maschinen";
        _machineCount.ForeColor = Color.FromArgb(148, 163, 184);
        _machineCount.Font = AppTheme.SmallFont;
        _machineCount.Location = new Point(72, 64);
        _machineCount.AutoSize = true;
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_machineCount);

        var quickActions = SidebarButtonGrid(3, height: 52, topPadding: 4, bottomPadding: 8);
        var add = AppTheme.SidebarButton("Neu", primary: true);
        var edit = AppTheme.SidebarButton("Bearbeiten");
        var adHoc = AppTheme.SidebarButton("Ad hoc");
        add.Click += (_, _) => AddMachine();
        edit.Click += (_, _) => EditMachine();
        adHoc.Click += (_, _) => ConnectAdHoc();
        AddSidebarButton(quickActions, add, 0);
        AddSidebarButton(quickActions, edit, 1);
        AddSidebarButton(quickActions, adHoc, 2);

        _search.PlaceholderText = "Suchen...";
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.BackColor = Color.FromArgb(31, 41, 55);
        _search.ForeColor = Color.White;
        _search.Margin = new Padding(0);
        _search.TextChanged += (_, _) => RefreshMachineList();
        var searchWrap = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = AppTheme.Sidebar,
            Padding = new Padding(6, 8, 6, 8),
        };
        _search.Dock = DockStyle.Fill;
        searchWrap.Controls.Add(_search);

        ConfigureMachineList(_connectedMachineList);
        ConfigureMachineList(_machineList);
        BuildMachineContextMenu();

        _connectedHeader.Dock = DockStyle.Top;
        _connectedHeader.Height = 24;
        _connectedHeader.Text = "VERBUNDEN";
        _connectedHeader.ForeColor = Color.FromArgb(100, 116, 139);
        _connectedHeader.Font = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold);
        _connectedHeader.Padding = new Padding(10, 7, 0, 0);
        _connectedHeader.BackColor = AppTheme.Sidebar;
        _connectedHeader.Visible = false;
        _connectedMachineList.Dock = DockStyle.Top;
        _connectedMachineList.Visible = false;
        _connectedMachineList.Paint += DrawConnectedSeparator;

        _machineListHost.Dock = DockStyle.Fill;
        _machineListHost.BackColor = AppTheme.Sidebar;
        _machineListHost.Padding = new Padding(0, 0, 8, 0);
        _machineListHost.Paint += DrawMachineListScrollIndicator;
        _machineListHost.Resize += (_, _) => LayoutMachineList();
        _machineListHost.Controls.Add(_machineList);
        _machineListHost.Controls.Add(_connectedMachineList);
        _machineListHost.Controls.Add(_connectedHeader);
        LayoutMachineList();

        var bottomActions = SidebarButtonGrid(1, height: 66, topPadding: 12, bottomPadding: 12);
        var setup = AppTheme.SidebarButton("Setup", primary: true);
        setup.Click += (_, _) => OpenSetup();
        AddSidebarButton(bottomActions, setup, 0);

        sidebar.Controls.Add(_machineListHost);
        sidebar.Controls.Add(bottomActions);
        sidebar.Controls.Add(searchWrap);
        sidebar.Controls.Add(quickActions);
        sidebar.Controls.Add(header);
        return sidebar;
    }

    private void ConfigureMachineList(ListBox list)
    {
        list.Dock = DockStyle.Fill;
        list.DisplayMember = nameof(MachineEntry.DisplayName);
        list.BorderStyle = BorderStyle.None;
        list.BackColor = AppTheme.Sidebar;
        list.ForeColor = Color.White;
        list.Font = AppTheme.UiFont;
        list.ItemHeight = 56;
        list.DrawMode = DrawMode.OwnerDrawFixed;
        list.IntegralHeight = false;
        list.DrawItem += DrawMachineItem;
        list.SelectedIndexChanged += MachineSelectionChanged;
        list.DoubleClick += (_, _) => ConnectSelected();
        list.MouseDown += SelectMachineForContextMenu;
        list.MouseMove += ShowMachineTooltip;
        list.MouseLeave += (_, _) => HideMachineTooltip();
        list.MouseWheel += (_, _) => _machineListHost.Invalidate();
    }

    private void DrawConnectedSeparator(object? sender, PaintEventArgs e)
    {
        if (!_connectedMachineList.Visible)
        {
            return;
        }

        using var pen = new Pen(Color.FromArgb(51, 65, 85), 1);
        e.Graphics.DrawLine(pen, 10, _connectedMachineList.Height - 5, _connectedMachineList.Width - 10, _connectedMachineList.Height - 5);
    }

    private void LayoutMachineList()
    {
        var hiddenScrollWidth = SystemInformation.VerticalScrollBarWidth + 4;
        _machineList.Dock = DockStyle.None;
        var normalTop = (_connectedHeader.Visible ? _connectedHeader.Height : 0) + (_connectedMachineList.Visible ? _connectedMachineList.Height : 0);
        _machineList.Location = new Point(0, normalTop);
        _machineList.Size = new Size(_machineListHost.ClientSize.Width + hiddenScrollWidth, Math.Max(0, _machineListHost.ClientSize.Height - normalTop));
        _connectedMachineList.Width = _machineListHost.ClientSize.Width + hiddenScrollWidth;
        _machineListHost.Invalidate();
    }

    private Panel BuildWorkspace()
    {
        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Window,
            Padding = new Padding(28, 0, 22, 0),
        };

        _rdpPanel.Dock = DockStyle.Fill;
        _rdpPanel.BackColor = Color.Black;
        _rdpPanel.Padding = new Padding(1);
        _rdpPanel.Resize += (_, _) => ActiveSession()?.ResizeToHost();

        _placeholder.Dock = DockStyle.Fill;
        _placeholder.Text = "Links eine Maschine auswählen und verbinden.";
        _placeholder.TextAlign = ContentAlignment.MiddleCenter;
        _placeholder.ForeColor = Color.FromArgb(226, 232, 240);
        _placeholder.Font = new Font("Segoe UI", 14f, FontStyle.Regular);
        _placeholder.BackColor = Color.FromArgb(15, 23, 42);
        _rdpPanel.Controls.Add(_placeholder);

        workspace.Controls.Add(_rdpPanel);
        return workspace;
    }

    private void BuildMachineContextMenu()
    {
        var connect = _machineMenu.Items.Add("Connect", null, (_, _) => ConnectSelected());
        var connectAs = _machineMenu.Items.Add("Verbinden als...", null, (_, _) => ConnectSelectedAs());
        _machineMenu.Items.Add(new ToolStripSeparator());
        var disconnect = _machineMenu.Items.Add("Abmelden", null, (_, _) => DisconnectSelected());
        var favorite = _machineMenu.Items.Add("Favorit umschalten", null, (_, _) => ToggleFavoriteSelected());
        var edit = _machineMenu.Items.Add("Bearbeiten", null, (_, _) => EditMachine());
        var delete = _machineMenu.Items.Add("Eintrag entfernen", null, (_, _) => DeleteMachine());
        var ping = _machineMenu.Items.Add("Ping -t", null, (_, _) => PingSelected());
        var reconnect = _machineMenu.Items.Add("Reconnect", null, (_, _) => ReconnectSelected());
        _machineMenu.Opening += (_, e) =>
        {
            var machine = SelectedMachine();
            if (machine is null)
            {
                e.Cancel = true;
                return;
            }

            SweepDisconnectedSessions(refreshUi: false, notifyFailures: false);
            var isConnected = IsSessionConnected(machine.Id);
            connect.Enabled = true;
            connectAs.Enabled = true;
            disconnect.Enabled = isConnected;
            favorite.Enabled = !machine.IsTemporary;
            edit.Enabled = !machine.IsTemporary;
            delete.Enabled = true;
            ping.Enabled = true;
            reconnect.Enabled = true;
        };
    }

    private static TableLayoutPanel SidebarButtonGrid(int columns, int height, int topPadding, int bottomPadding, int rows = 1)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = height,
            BackColor = AppTheme.Sidebar,
            ColumnCount = columns,
            RowCount = rows,
            Padding = new Padding(0, topPadding, 0, bottomPadding),
        };

        for (var column = 0; column < columns; column++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        }

        for (var row = 0; row < rows; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        }

        return grid;
    }

    private static void AddSidebarButton(TableLayoutPanel grid, Button button, int column, int row = 0)
    {
        grid.Controls.Add(button, column, row);
    }

    private void SelectMachineForContextMenu(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        if (sender is not ListBox list)
        {
            return;
        }

        var index = list.IndexFromPoint(e.Location);
        if (index < 0)
        {
            return;
        }

        list.SelectedIndex = index;
        _machineMenu.Show(list, e.Location);
    }

    private void ShowMachineTooltip(object? sender, MouseEventArgs e)
    {
        if (sender is not ListBox list)
        {
            HideMachineTooltip();
            return;
        }

        var index = list.IndexFromPoint(e.Location);
        if (index < 0 || index >= list.Items.Count)
        {
            HideMachineTooltip();
            return;
        }

        var machine = (MachineEntry)list.Items[index];
        if (_toolTipMachineId == machine.Id)
        {
            return;
        }

        _toolTipMachineId = machine.Id;
        _machineToolTip.SetToolTip(list, MachineTooltip(machine));
    }

    private void HideMachineTooltip()
    {
        _toolTipMachineId = null;
        _machineToolTip.SetToolTip(_machineList, "");
        _machineToolTip.SetToolTip(_connectedMachineList, "");
    }

    private string MachineTooltip(MachineEntry machine)
    {
        var credential = CredentialFor(machine)?.DisplayName ?? "Aktueller Windows-Benutzer";
        var group = GroupFor(machine);
        var redirects = EffectiveRedirectSettings(machine);
        var flags = new List<string>();
        if (redirects.Clipboard)
        {
            flags.Add("Zwischenablage");
        }
        if (redirects.Printers)
        {
            flags.Add("Drucker");
        }
        if (redirects.SmartCards)
        {
            flags.Add("Smartcards");
        }
        if (redirects.WebAuthn)
        {
            flags.Add("WebAuthn");
        }

        var lines = new List<string>
        {
            machine.DisplayName,
            $"Host: {machine.DnsName}",
        };
        if (group is not null)
        {
            lines.Add($"Gruppe: {group.DisplayName}");
        }
        lines.Add($"Zugang: {credential}");
        lines.Add($"Freigaben: {(flags.Count == 0 ? "keine" : string.Join(", ", flags))}");
        lines.Add($"Freigaben-Modus: {(machine.UseGlobalRedirectSettings != false ? "global" : "Rechner")}");
        if (!string.IsNullOrWhiteSpace(machine.Notes))
        {
            lines.Add("");
            lines.Add(machine.Notes);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void DrawMachineItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox list || e.Index < 0 || e.Index >= list.Items.Count)
        {
            return;
        }

        var machine = (MachineEntry)list.Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var isConnected = IsSessionConnected(machine.Id);
        var bounds = Rectangle.Inflate(e.Bounds, -2, -4);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var background = new SolidBrush(machine.IsTemporary
            ? selected ? Color.FromArgb(22, 78, 99) : Color.FromArgb(19, 50, 60)
            : selected ? AppTheme.SidebarAlt : AppTheme.Sidebar);
        using var accent = new SolidBrush(machine.IsTemporary ? Color.FromArgb(20, 184, 166) : MachineColor(machine, isConnected));
        using var title = new SolidBrush(Color.White);
        using var muted = new SolidBrush(machine.IsTemporary ? Color.FromArgb(153, 246, 228) : Color.FromArgb(148, 163, 184));
        using var badgeBackground = new SolidBrush(Color.FromArgb(34, 197, 94));
        using var badgeText = new SolidBrush(Color.FromArgb(5, 46, 22));
        using var titleFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        using var badgeFont = new Font("Segoe UI Semibold", 7.2f, FontStyle.Bold);

        e.Graphics.FillRoundedRectangle(background, bounds, 8);

        e.Graphics.FillEllipse(accent, bounds.Left + 12, bounds.Top + 14, 24, 24);
        var displayName = machine.IsFavorite ? $"* {machine.DisplayName}" : machine.DisplayName;
        e.Graphics.DrawString(displayName, titleFont, title, bounds.Left + 48, bounds.Top + 8);

        var secondary = machine.IsTemporary
            ? $"Ad hoc - {machine.DnsName}"
            : GroupFor(machine) is not { } group ? machine.DnsName : $"{group.DisplayName} - {machine.DnsName}";
        e.Graphics.DrawString(secondary, AppTheme.SmallFont, muted, bounds.Left + 48, bounds.Top + 29);

        if (isConnected)
        {
            var badge = new Rectangle(bounds.Right - 48, bounds.Top + 16, 38, 18);
            e.Graphics.FillRoundedRectangle(badgeBackground, badge, 6);
            e.Graphics.DrawString("LIVE", badgeFont, badgeText, badge.Left + 7, badge.Top + 3);
        }
    }

    private void DrawMachineListScrollIndicator(object? sender, PaintEventArgs e)
    {
        if (_machineList.Items.Count == 0 || _machineList.ItemHeight <= 0 || _machineList.Height <= 0)
        {
            return;
        }

        var visibleItems = Math.Max(1, _machineList.Height / _machineList.ItemHeight);
        if (_machineList.Items.Count <= visibleItems)
        {
            return;
        }

        const int width = 4;
        var track = new Rectangle(
            _machineListHost.ClientSize.Width - width,
            _machineList.Top + 8,
            width,
            Math.Max(1, _machineList.Height - 16));
        var thumbHeight = Math.Max(34, track.Height * visibleItems / _machineList.Items.Count);
        var maxTopIndex = Math.Max(1, _machineList.Items.Count - visibleItems);
        var top = track.Top + (track.Height - thumbHeight) * Math.Min(_machineList.TopIndex, maxTopIndex) / maxTopIndex;
        var thumb = new Rectangle(track.Left, top, track.Width, thumbHeight);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var trackBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
        using var thumbBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
        e.Graphics.FillRoundedRectangle(trackBrush, track, 2);
        e.Graphics.FillRoundedRectangle(thumbBrush, thumb, 2);
    }

    private void LoadData()
    {
        _data = _store.Load();
        NormalizeData();
    }

    private void NormalizeData()
    {
        if (!_data.SetupWizardCompleted && (_data.Machines.Count > 0 || _data.Credentials.Count > 0))
        {
            _data.SetupWizardCompleted = true;
        }

        foreach (var machine in _data.Machines)
        {
            if (machine.UseGlobalRedirectSettings is null || HasLegacyDefaultRedirectSettings(machine))
            {
                machine.UseGlobalRedirectSettings = true;
            }

            if (machine.GroupId is not null || string.IsNullOrWhiteSpace(machine.GroupName))
            {
                continue;
            }

            var group = _data.Groups.FirstOrDefault(item => item.Name.Equals(machine.GroupName, StringComparison.OrdinalIgnoreCase));
            if (group is null)
            {
                group = new MachineGroup
                {
                    Name = machine.GroupName.Trim(),
                    ColorKey = string.IsNullOrWhiteSpace(machine.ColorKey) ? "blue" : machine.ColorKey,
                };
                _data.Groups.Add(group);
            }

            machine.GroupId = group.Id;
        }
    }

    private static bool HasLegacyDefaultRedirectSettings(MachineEntry machine)
    {
        return machine.UseGlobalRedirectSettings == false
            && !machine.RedirectClipboard
            && !machine.RedirectPrinters
            && !machine.RedirectSmartCards
            && !machine.RedirectWebAuthn;
    }

    private Color MachineColor(MachineEntry machine, bool isConnected)
    {
        return ColorPalette.Marker(EffectiveColorKey(machine), isConnected);
    }

    private string EffectiveColorKey(MachineEntry machine)
    {
        if (!string.IsNullOrWhiteSpace(machine.ColorKey))
        {
            return machine.ColorKey;
        }

        return GroupFor(machine)?.ColorKey ?? "blue";
    }

    private void SaveData()
    {
        _store.Save(_data);
    }

    private void HeaderMouseClick(object? sender, MouseEventArgs e)
    {
        var logoBounds = new Rectangle(0, 6, 56, 56);
        if (!logoBounds.Contains(e.Location))
        {
            return;
        }

        var now = DateTime.UtcNow;
        _logoClickCount = now - _lastLogoClickAt > TimeSpan.FromSeconds(3)
            ? 1
            : _logoClickCount + 1;
        _lastLogoClickAt = now;

        if (_logoClickCount < 5)
        {
            return;
        }

        _logoClickCount = 0;
        OpenSetupWizard(force: true);
    }

    private void OpenSetupWizardIfNeeded()
    {
        if (_data.SetupWizardCompleted)
        {
            return;
        }

        OpenSetupWizard(force: false);
    }

    private void OpenSetupWizard(bool force)
    {
        if (!force && _data.SetupWizardCompleted)
        {
            return;
        }

        using var dialog = new SetupWizardForm(_data);
        dialog.ShowDialog(this);
        if (dialog.DataChanged)
        {
            SaveData();
            NormalizeData();
            RefreshMachineList();
            ShowSelectedSession();
        }
    }

    private void RememberSession(Guid machineId)
    {
        if (!_data.RememberConnectedSessions)
        {
            return;
        }

        if (IsTemporaryMachine(machineId))
        {
            return;
        }

        if (!_data.AutoReconnectMachineIds.Contains(machineId))
        {
            _data.AutoReconnectMachineIds.Add(machineId);
            SaveData();
        }
    }

    private void ForgetSession(Guid machineId)
    {
        if (_data.AutoReconnectMachineIds.RemoveAll(id => id == machineId) > 0)
        {
            SaveData();
        }
    }

    private void RememberConnectedSessions()
    {
        if (!_data.RememberConnectedSessions)
        {
            _data.AutoReconnectMachineIds.Clear();
            return;
        }

        var currentMachineIds = _data.Machines.Select(machine => machine.Id).ToHashSet();
        var openSessionIds = _sessions.Keys
            .Where(machineId => currentMachineIds.Contains(machineId) && !IsTemporaryMachine(machineId));

        _data.AutoReconnectMachineIds = _data.AutoReconnectMachineIds
            .Concat(openSessionIds)
            .Where(machineId => currentMachineIds.Contains(machineId))
            .Distinct()
            .ToList();
    }

    private bool IsTemporaryMachine(Guid machineId)
    {
        return _temporaryMachines.Any(machine => machine.Id == machineId);
    }

    private void RestoreRememberedSessions()
    {
        if (_restoredRememberedSessions)
        {
            return;
        }

        _restoredRememberedSessions = true;
        if (!_data.RestoreConnectedSessionsOnStart)
        {
            return;
        }

        var rememberedIds = _data.AutoReconnectMachineIds
            .Distinct()
            .ToList();
        if (rememberedIds.Count == 0)
        {
            return;
        }

        var machines = rememberedIds
            .Select(id => _data.Machines.FirstOrDefault(machine => machine.Id == id))
            .Where(machine => machine is not null)
            .Cast<MachineEntry>()
            .ToList();
        if (machines.Count == 0)
        {
            _data.AutoReconnectMachineIds.Clear();
            SaveData();
            return;
        }

        _statusLabel.Text = $"Stelle {machines.Count} Session(s) wieder her";
        foreach (var machine in machines)
        {
            SelectMachine(machine.Id);
            ConnectMachine(machine, CredentialFor(machine), showErrors: false);
        }
    }

    private void RefreshMachineList()
    {
        var selectedId = _selectedMachineId ?? SelectedMachine()?.Id;
        var filter = _search.Text.Trim();
        var allMachines = _data.Machines
            .Concat(_temporaryMachines)
            .Where(machine => MatchesFilter(machine, filter))
            .ToList();
        var connectedMachines = allMachines
            .Where(machine => IsSessionConnected(machine.Id))
            .OrderByDescending(HasGroup)
            .ThenBy(GroupSortName)
            .ThenByDescending(machine => machine.IsTemporary)
            .ThenByDescending(machine => machine.IsFavorite)
            .ThenBy(machine => machine.DisplayName)
            .ToList();
        var machines = allMachines
            .Where(machine => !IsSessionConnected(machine.Id))
            .OrderByDescending(HasGroup)
            .ThenBy(GroupSortName)
            .ThenByDescending(machine => machine.IsTemporary)
            .ThenByDescending(machine => machine.IsFavorite)
            .ThenBy(machine => machine.DisplayName)
            .ToList();

        _connectedMachineList.DataSource = null;
        _connectedMachineList.DataSource = connectedMachines;
        _machineList.DataSource = null;
        _machineList.DataSource = machines;
        _connectedHeader.Visible = connectedMachines.Count > 0;
        _connectedMachineList.Visible = connectedMachines.Count > 0;
        _connectedMachineList.Height = connectedMachines.Count == 0
            ? 0
            : connectedMachines.Count * _connectedMachineList.ItemHeight + 10;
        var countText = _data.Machines.Count == 1 ? "1 Maschine" : $"{_data.Machines.Count} Maschinen";
        _machineCount.Text = _temporaryMachines.Count == 0 ? countText : $"{countText}, {_temporaryMachines.Count} ad hoc";
        if (selectedId is not null)
        {
            SelectMachine(selectedId.Value);
        }
        LayoutMachineList();
        _machineListHost.Invalidate();
    }

    private bool MatchesFilter(MachineEntry machine, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return Contains(machine.DisplayName, filter)
            || Contains(machine.DnsName, filter)
            || Contains(machine.GroupName, filter)
            || Contains(GroupFor(machine)?.DisplayName, filter)
            || Contains(machine.Notes, filter);
    }

    private bool HasGroup(MachineEntry machine)
    {
        return GroupFor(machine) is not null;
    }

    private string GroupSortName(MachineEntry machine)
    {
        return GroupFor(machine)?.DisplayName ?? "";
    }

    private static bool Contains(string? value, string filter)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectMachine(Guid machineId)
    {
        _syncingMachineSelection = true;
        try
        {
            _connectedMachineList.ClearSelected();
            _machineList.ClearSelected();
            foreach (var item in _connectedMachineList.Items)
            {
                if (item is MachineEntry machine && machine.Id == machineId)
                {
                    _connectedMachineList.SelectedItem = machine;
                    _selectedMachineId = machineId;
                    return;
                }
            }

            foreach (var item in _machineList.Items)
            {
                if (item is MachineEntry machine && machine.Id == machineId)
                {
                    _machineList.SelectedItem = machine;
                    _selectedMachineId = machineId;
                    return;
                }
            }
            _selectedMachineId = null;
        }
        finally
        {
            _syncingMachineSelection = false;
        }
    }

    private MachineEntry? SelectedMachine()
    {
        if (_selectedMachineId is not null)
        {
            foreach (var item in _connectedMachineList.Items.Cast<MachineEntry>().Concat(_machineList.Items.Cast<MachineEntry>()))
            {
                if (item.Id == _selectedMachineId)
                {
                    return item;
                }
            }
        }

        return _connectedMachineList.SelectedItem as MachineEntry ?? _machineList.SelectedItem as MachineEntry;
    }

    private void MachineSelectionChanged(object? sender, EventArgs e)
    {
        if (_syncingMachineSelection || sender is not ListBox list || list.SelectedItem is not MachineEntry machine)
        {
            return;
        }

        _syncingMachineSelection = true;
        try
        {
            if (!ReferenceEquals(list, _connectedMachineList))
            {
                _connectedMachineList.ClearSelected();
            }
            if (!ReferenceEquals(list, _machineList))
            {
                _machineList.ClearSelected();
            }
            _selectedMachineId = machine.Id;
        }
        finally
        {
            _syncingMachineSelection = false;
        }

        ShowSelectedSession();
        _machineListHost.Invalidate();
    }

    private IRemoteSessionHost? ActiveSession()
    {
        var machine = SelectedMachine();
        return machine is not null && TryGetLiveSession(machine.Id, out var session) ? session : null;
    }

    private bool TryGetLiveSession(Guid machineId, out IRemoteSessionHost session)
    {
        if (!_sessions.TryGetValue(machineId, out session!))
        {
            return false;
        }

        if (session.IsConnected)
        {
            MarkSessionConnected(machineId);
            return true;
        }

        if (IsSessionStarting(machineId))
        {
            return true;
        }

        CleanupDeadSession(machineId, session, notifyFailure: true);
        return false;
    }

    private bool IsSessionConnected(Guid machineId)
    {
        if (!_sessions.TryGetValue(machineId, out var session))
        {
            return false;
        }

        if (!session.IsConnected)
        {
            return false;
        }

        MarkSessionConnected(machineId);
        return true;
    }

    private bool IsSessionStarting(Guid machineId)
    {
        return _sessionStartedAt.TryGetValue(machineId, out var startedAt)
            && DateTime.UtcNow - startedAt < ConnectionStartupGrace;
    }

    private bool MarkSessionConnected(Guid machineId)
    {
        var newlyConnected = !_sessionWasConnected.Contains(machineId);
        _sessionStartedAt.Remove(machineId);
        _sessionWasConnected.Add(machineId);
        RememberSession(machineId);
        return newlyConnected;
    }

    private void SweepDisconnectedSessions(bool refreshUi = true, bool notifyFailures = true)
    {
        var selectedId = SelectedMachine()?.Id;
        var changedAny = false;

        foreach (var (machineId, session) in _sessions.ToList())
        {
            if (session.IsConnected)
            {
                changedAny |= MarkSessionConnected(machineId);
                continue;
            }

            if (IsSessionStarting(machineId))
            {
                continue;
            }

            CleanupDeadSession(machineId, session, notifyFailures);
            changedAny = true;
        }

        if (!changedAny || !refreshUi)
        {
            return;
        }

        RefreshMachineList();
        if (selectedId is not null)
        {
            SelectMachine(selectedId.Value);
        }
        ShowSelectedSession();
    }

    private void CleanupDeadSession(Guid machineId, IRemoteSessionHost session, bool notifyFailure)
    {
        var machine = session.Machine;
        var wasConnected = _sessionWasConnected.Remove(machineId);
        var hadStartup = _sessionStartedAt.Remove(machineId);
        var showError = _sessionShowErrors.Remove(machineId, out var shouldShow) && shouldShow;

        session.Dispose();
        _sessions.Remove(machineId);
        if (!wasConnected)
        {
            ForgetSession(machineId);
        }
        _temporaryMachines.RemoveAll(machine => machine.Id == machineId);

        if (notifyFailure && showError && hadStartup && !wasConnected)
        {
            NotifyConnectionFailed(machine);
        }
    }

    private void NotifyConnectionFailed(MachineEntry machine)
    {
        _statusLabel.Text = $"Verbindung fehlgeschlagen: {machine.DisplayName}";
        MessageBox.Show(
            this,
            $"Die RDP-Verbindung zu \"{machine.DisplayName}\" wurde nicht hergestellt.\n\nDer Zielrechner ist eventuell nicht erreichbar, RDP ist deaktiviert, die Anmeldung wurde abgebrochen oder die Sicherheitsabfrage wurde nicht bestätigt.",
            Brand.AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private CredentialProfile? CredentialFor(MachineEntry machine)
    {
        var credentialId = machine.CredentialProfileId
            ?? GroupFor(machine)?.CredentialProfileId
            ?? _data.GlobalCredentialProfileId;

        return credentialId is null
            ? null
            : _data.Credentials.FirstOrDefault(credential => credential.Id == credentialId);
    }

    private MachineGroup? GroupFor(MachineEntry machine)
    {
        return machine.GroupId is null
            ? null
            : _data.Groups.FirstOrDefault(group => group.Id == machine.GroupId);
    }

    private void ShowSelectedSession()
    {
        _rdpPanel.Controls.Clear();
        var session = ActiveSession();
        if (session is null)
        {
            _rdpPanel.Controls.Add(_placeholder);
            _placeholder.Text = SelectedMachine() is null
                ? "Links eine Maschine auswählen und verbinden."
                : "Noch nicht verbunden. Connect startet die RDP-Session.";
            _statusLabel.Text = SelectedMachine()?.DnsName ?? "Bereit";
            return;
        }

        ShowSessionControl(session);
    }

    private void ShowSessionControl(IRemoteSessionHost session)
    {
        _rdpPanel.Controls.Clear();
        _rdpPanel.Controls.Add(session.Control);
        session.Control.Visible = true;
        session.Control.BringToFront();
        session.ResizeToHost();
        _statusLabel.Text = $"{session.Machine.DisplayName} verbunden";
    }

    private void ConnectSelected()
    {
        var machine = SelectedMachine();
        if (machine is null)
        {
            return;
        }

        if (_sessions.ContainsKey(machine.Id))
        {
            ReconnectMachine(machine);
            return;
        }

        ConnectMachine(machine, CredentialFor(machine), showErrors: true);
    }

    private void ConnectSelectedAs()
    {
        var machine = SelectedMachine();
        if (machine is null)
        {
            return;
        }

        using var dialog = new ConnectAsForm(_data, $"Verbinden als: {machine.DisplayName}", allowRememberDefault: false);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ConnectMachine(machine, dialog.SelectedCredential, showErrors: true, replaceExistingSession: true);
    }

    private void ConnectAdHoc()
    {
        using var dialog = new ConnectAsForm(_data, "Ad hoc verbinden", allowRememberDefault: false, showHost: true);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var machine = new MachineEntry
        {
            Name = dialog.HostName,
            DnsName = dialog.HostName,
            UseGlobalRedirectSettings = true,
            IsTemporary = true,
        };
        _temporaryMachines.Add(machine);
        RefreshMachineList();
        SelectMachine(machine.Id);
        ConnectMachine(machine, dialog.SelectedCredential, showErrors: true, replaceExistingSession: true);
    }

    private bool ConnectMachine(
        MachineEntry machine,
        CredentialProfile? credential,
        bool showErrors,
        bool replaceExistingSession = false)
    {
        try
        {
            if (replaceExistingSession && _sessions.TryGetValue(machine.Id, out var existingSession))
            {
                existingSession.Dispose();
                _sessions.Remove(machine.Id);
                _sessionStartedAt.Remove(machine.Id);
                _sessionShowErrors.Remove(machine.Id);
                _sessionWasConnected.Remove(machine.Id);
            }

            if (!_sessions.TryGetValue(machine.Id, out var session))
            {
                session = CreateSessionHost(machine, credential);
                _sessions[machine.Id] = session;
                _sessionStartedAt[machine.Id] = DateTime.UtcNow;
                _sessionShowErrors[machine.Id] = showErrors;
                _sessionWasConnected.Remove(machine.Id);
            }

            ShowSessionControl(session);
            session.Connect();
            SelectMachine(machine.Id);
            RefreshMachineList();
            SelectMachine(machine.Id);
            ShowSessionControl(session);
            BeginInvoke(new Action(() =>
            {
                if (_sessions.TryGetValue(machine.Id, out var currentSession) && ReferenceEquals(currentSession, session))
                {
                    SelectMachine(machine.Id);
                    ShowSessionControl(currentSession);
                }
            }));
            _statusLabel.Text = $"Verbinde mit {machine.DisplayName}";
            return true;
        }
        catch (Exception ex)
        {
            if (_sessions.TryGetValue(machine.Id, out var failedSession))
            {
                failedSession.Dispose();
                _sessions.Remove(machine.Id);
                _sessionStartedAt.Remove(machine.Id);
                _sessionShowErrors.Remove(machine.Id);
                _sessionWasConnected.Remove(machine.Id);
            }
            ForgetSession(machine.Id);
            RemoveTemporaryMachine(machine.Id);
            ShowSelectedSession();
            _statusLabel.Text = $"Verbindung fehlgeschlagen: {machine.DisplayName}";
            if (showErrors)
            {
                MessageBox.Show(
                    this,
                    $"Die RDP-Verbindung zu \"{machine.DisplayName}\" konnte nicht gestartet werden.\n\n{ex.Message}",
                    Brand.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return false;
        }
    }

    private IRemoteSessionHost CreateSessionHost(MachineEntry machine, CredentialProfile? credential)
    {
        return new RdpSessionHost(MachineWithEffectiveRedirects(machine), credential);
    }

    private MachineEntry MachineWithEffectiveRedirects(MachineEntry machine)
    {
        var redirects = EffectiveRedirectSettings(machine);
        return new MachineEntry
        {
            Id = machine.Id,
            Name = machine.Name,
            DnsName = machine.DnsName,
            GroupId = machine.GroupId,
            GroupName = machine.GroupName,
            ColorKey = machine.ColorKey,
            Notes = machine.Notes,
            CredentialProfileId = machine.CredentialProfileId,
            IsFavorite = machine.IsFavorite,
            UseGlobalRedirectSettings = machine.UseGlobalRedirectSettings,
            RedirectClipboard = redirects.Clipboard,
            RedirectPrinters = redirects.Printers,
            RedirectSmartCards = redirects.SmartCards,
            RedirectWebAuthn = redirects.WebAuthn,
            IsTemporary = machine.IsTemporary,
        };
    }

    private RedirectSettings EffectiveRedirectSettings(MachineEntry machine)
    {
        return machine.UseGlobalRedirectSettings != false
            ? new RedirectSettings(
                _data.GlobalRedirectClipboard,
                _data.GlobalRedirectPrinters,
                _data.GlobalRedirectSmartCards,
                _data.GlobalRedirectWebAuthn)
            : new RedirectSettings(
                machine.RedirectClipboard,
                machine.RedirectPrinters,
                machine.RedirectSmartCards,
                machine.RedirectWebAuthn);
    }

    private void RemoveTemporaryMachine(Guid machineId)
    {
        if (_temporaryMachines.RemoveAll(machine => machine.Id == machineId) > 0)
        {
            RefreshMachineList();
        }
    }

    private void DisposeSession(Guid machineId)
    {
        if (_sessions.TryGetValue(machineId, out var session))
        {
            session.Dispose();
            _sessions.Remove(machineId);
            _sessionStartedAt.Remove(machineId);
            _sessionShowErrors.Remove(machineId);
            _sessionWasConnected.Remove(machineId);
        }
    }

    private void ReconnectSelected()
    {
        var machine = SelectedMachine();
        if (machine is null)
        {
            return;
        }

        ReconnectMachine(machine);
    }

    private void ReconnectMachine(MachineEntry machine)
    {
        try
        {
            if (ConnectMachine(machine, CredentialFor(machine), showErrors: true, replaceExistingSession: true))
            {
                _machineList.Invalidate();
                _connectedMachineList.Invalidate();
                _statusLabel.Text = $"Reconnect: {machine.DisplayName}";
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Reconnect fehlgeschlagen: {machine.DisplayName}";
            MessageBox.Show(
                this,
                $"Reconnect zu \"{machine.DisplayName}\" ist fehlgeschlagen.\n\n{ex.Message}",
                Brand.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void DisconnectSelected()
    {
        var machine = SelectedMachine();
        if (machine is null || !_sessions.TryGetValue(machine.Id, out var session))
        {
            return;
        }

        session.Dispose();
        _sessions.Remove(machine.Id);
        _sessionStartedAt.Remove(machine.Id);
        _sessionShowErrors.Remove(machine.Id);
        _sessionWasConnected.Remove(machine.Id);
        ForgetSession(machine.Id);
        RemoveTemporaryMachine(machine.Id);
        ShowSelectedSession();
        _machineList.Invalidate();
        _statusLabel.Text = $"Getrennt: {machine.DisplayName}";
    }

    private void ToggleFavoriteSelected()
    {
        var machine = SelectedMachine();
        if (machine is null || machine.IsTemporary)
        {
            return;
        }

        machine.IsFavorite = !machine.IsFavorite;
        SaveData();
        RefreshMachineList();
        SelectMachine(machine.Id);
    }

    private void PingSelected()
    {
        var machine = SelectedMachine();
        if (machine is null)
        {
            return;
        }

        var process = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k ping -t {machine.DnsName}",
            UseShellExecute = true,
        };
        Process.Start(process);
    }

    private void OpenSetup()
    {
        using var dialog = new SetupForm(
            _data,
            ExportBackup,
            () =>
            {
                ImportBackup();
                RefreshMachineList();
            });
        if (dialog.ShowDialog(this) == DialogResult.OK || dialog.DataChanged)
        {
            SaveData();
            RefreshMachineList();
        }
    }

    private void ExportBackup()
    {
        using var dialog = new SaveFileDialog
        {
            Title = $"{Brand.AppName} Backup erstellen",
            FileName = $"lordofthepings-backup-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Filter = Brand.BackupFilter,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SaveData();
            _store.ExportBackup(_data, dialog.FileName);
            _statusLabel.Text = "Backup erstellt";
            MessageBox.Show(this, "Backup wurde erstellt.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Backup fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportBackup()
    {
        using var dialog = new OpenFileDialog
        {
            Title = $"{Brand.AppName} Backup wiederherstellen",
            Filter = Brand.BackupFilter,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "Das Backup ersetzt die aktuelle Maschinen- und Zugangsliste. Bestehende RDP-Sitzungen werden getrennt.\n\nPasswoerter sind mit Windows DPAPI geschuetzt und muessen auf einem anderen Windows-Benutzer oder Rechner eventuell neu gesetzt werden.",
            "Backup wiederherstellen",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);
        if (result != DialogResult.OK)
        {
            return;
        }

        try
        {
            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }
            _sessions.Clear();

            _data = _store.ImportBackup(dialog.FileName);
            SaveData();
            RefreshMachineList();
            ShowSelectedSession();
            _statusLabel.Text = "Backup wiederhergestellt";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Restore fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddMachine()
    {
        using var dialog = new MachineEditForm(_data, null);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Machine is null)
        {
            return;
        }

        _data.Machines.Add(dialog.Machine);
        SaveData();
        RefreshMachineList();
    }

    private void EditMachine()
    {
        var machine = SelectedMachine();
        if (machine is null)
        {
            return;
        }
        if (machine.IsTemporary)
        {
            MessageBox.Show(this, "Ad-hoc-Verbindungen sind nur temporÃ¤r und kÃ¶nnen nicht bearbeitet werden.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new MachineEditForm(_data, machine);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Machine is null)
        {
            return;
        }

        machine.Name = dialog.Machine.Name;
        machine.DnsName = dialog.Machine.DnsName;
        machine.GroupId = dialog.Machine.GroupId;
        machine.GroupName = dialog.Machine.GroupName;
        machine.ColorKey = dialog.Machine.ColorKey;
        machine.Notes = dialog.Machine.Notes;
        machine.CredentialProfileId = dialog.Machine.CredentialProfileId;
        machine.IsFavorite = dialog.Machine.IsFavorite;
        machine.UseGlobalRedirectSettings = dialog.Machine.UseGlobalRedirectSettings;
        machine.RedirectClipboard = dialog.Machine.RedirectClipboard;
        machine.RedirectPrinters = dialog.Machine.RedirectPrinters;
        machine.RedirectSmartCards = dialog.Machine.RedirectSmartCards;
        machine.RedirectWebAuthn = dialog.Machine.RedirectWebAuthn;
        SaveData();
        RefreshMachineList();
    }

    private void DeleteMachine()
    {
        var machine = SelectedMachine();
        if (machine is null)
        {
            return;
        }

        if (!machine.IsTemporary)
        {
            var result = MessageBox.Show(
                this,
                $"\"{machine.DisplayName}\" wirklich aus der Liste entfernen?\n\nEine laufende Verbindung wird getrennt.",
                "Eintrag entfernen",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        var machineId = machine.Id;
        DisposeSession(machineId);
        ForgetSession(machineId);

        if (machine.IsTemporary)
        {
            _temporaryMachines.RemoveAll(item => item.Id == machineId);
        }
        else
        {
            _data.Machines.RemoveAll(item => item.Id == machineId);
            SaveData();
        }

        RefreshMachineList();
        ShowSelectedSession();
        _statusLabel.Text = $"Entfernt: {machine.DisplayName}";
    }

    private void ManageCredentials()
    {
        using var dialog = new CredentialManagerForm(_data);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SaveData();
            RefreshMachineList();
        }
    }

    private void ManageGroups()
    {
        using var dialog = new GroupManagerForm(_data);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SaveData();
            RefreshMachineList();
        }
    }

    private void ImportFromAd()
    {
        using var dialog = new AdImportForm();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var imported = 0;
        var existingDns = _data.Machines.Select(machine => machine.DnsName.ToLowerInvariant()).ToHashSet();
        foreach (var dnsName in dialog.ImportedDnsNames)
        {
            if (existingDns.Contains(dnsName.ToLowerInvariant()))
            {
                continue;
            }

            _data.Machines.Add(new MachineEntry
            {
                Name = dnsName,
                DnsName = dnsName,
                UseGlobalRedirectSettings = true,
            });
            existingDns.Add(dnsName.ToLowerInvariant());
            imported++;
        }

        SaveData();
        RefreshMachineList();
        _statusLabel.Text = $"{imported} Maschinen importiert";
    }

    private sealed record RedirectSettings(bool Clipboard, bool Printers, bool SmartCards, bool WebAuthn);
}
