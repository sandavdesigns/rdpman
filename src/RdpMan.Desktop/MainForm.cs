using System.Diagnostics;

namespace RdpMan.Desktop;

public sealed class MainForm : Form
{
    private readonly DataStore _store = new();
    private readonly Dictionary<Guid, IRemoteSessionHost> _sessions = [];
    private readonly List<MachineEntry> _temporaryMachines = [];
    private bool _restoredRememberedSessions;
    private AppData _data = new();

    private readonly ListBox _machineList = new();
    private readonly Label _machineCount = new();
    private readonly Panel _rdpPanel = new();
    private readonly Label _placeholder = new();
    private readonly ContextMenuStrip _machineMenu = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    public MainForm()
    {
        Text = "RDP Man";
        MinimumSize = new Size(1120, 720);
        Width = 1340;
        Height = 840;
        AppTheme.ApplyWindow(this);

        BuildLayout();
        LoadData();
        RefreshMachineList();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RestoreRememberedSessions();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
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
        _statusLabel.Text = "Bereit";
        _statusLabel.ForeColor = AppTheme.MutedText;

        Controls.Add(root);
        Controls.Add(_statusStrip);
        _statusStrip.Dock = DockStyle.Bottom;
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

        var title = new Label
        {
            Text = "RDP Man",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
            Location = new Point(70, 10),
            AutoSize = true,
        };
        var subtitle = new Label
        {
            Text = "Remote Desktop Manager",
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

        var quickActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = AppTheme.Sidebar,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        var add = AppTheme.Button("Neu", primary: true);
        var edit = AppTheme.Button("Bearbeiten");
        var adHoc = AppTheme.Button("Ad hoc");
        add.Width = 74;
        edit.Width = 92;
        adHoc.Width = 78;
        add.Click += (_, _) => AddMachine();
        edit.Click += (_, _) => EditMachine();
        adHoc.Click += (_, _) => ConnectAdHoc();
        quickActions.Controls.Add(add);
        quickActions.Controls.Add(edit);
        quickActions.Controls.Add(adHoc);
        quickActions.Resize += (_, _) => CenterButtonRow(quickActions, 0);

        _machineList.Dock = DockStyle.Fill;
        _machineList.DisplayMember = nameof(MachineEntry.DisplayName);
        _machineList.BorderStyle = BorderStyle.None;
        _machineList.BackColor = AppTheme.Sidebar;
        _machineList.ForeColor = Color.White;
        _machineList.Font = AppTheme.UiFont;
        _machineList.ItemHeight = 56;
        _machineList.DrawMode = DrawMode.OwnerDrawFixed;
        _machineList.IntegralHeight = false;
        _machineList.DrawItem += DrawMachineItem;
        _machineList.SelectedIndexChanged += (_, _) => ShowSelectedSession();
        _machineList.DoubleClick += (_, _) => ConnectSelected();
        _machineList.MouseDown += SelectMachineForContextMenu;
        BuildMachineContextMenu();

        var bottomActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 92,
            BackColor = AppTheme.Sidebar,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 14, 0, 0),
        };
        var credentials = AppTheme.Button("Zugänge");
        var ad = AppTheme.Button("AD Import");
        var backup = AppTheme.Button("Backup");
        credentials.Width = 86;
        ad.Width = 86;
        backup.Width = 86;
        credentials.Click += (_, _) => ManageCredentials();
        ad.Click += (_, _) => ImportFromAd();
        backup.Click += (_, _) => ShowBackupMenu(backup);
        bottomActions.Controls.Add(credentials);
        bottomActions.Controls.Add(ad);
        bottomActions.Controls.Add(backup);
        bottomActions.Resize += (_, _) => CenterButtonRow(bottomActions, 14);

        sidebar.Controls.Add(_machineList);
        sidebar.Controls.Add(bottomActions);
        sidebar.Controls.Add(quickActions);
        sidebar.Controls.Add(header);
        return sidebar;
    }

    private Panel BuildWorkspace()
    {
        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Window,
            Padding = new Padding(22),
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

            var isConnected = _sessions.TryGetValue(machine.Id, out var session) && session.IsConnected;
            connect.Enabled = true;
            connectAs.Enabled = true;
            disconnect.Enabled = isConnected;
            edit.Enabled = !machine.IsTemporary;
            delete.Enabled = true;
            ping.Enabled = true;
            reconnect.Enabled = true;
        };
    }

    private static void CenterButtonRow(FlowLayoutPanel row, int topPadding)
    {
        var contentWidth = row.Controls.Cast<Control>().Sum(control => control.Width + control.Margin.Horizontal);
        var leftPadding = Math.Max(0, (row.ClientSize.Width - contentWidth) / 2);
        row.Padding = new Padding(leftPadding, topPadding, 0, 0);
    }

    private void SelectMachineForContextMenu(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var index = _machineList.IndexFromPoint(e.Location);
        if (index < 0)
        {
            return;
        }

        _machineList.SelectedIndex = index;
        _machineMenu.Show(_machineList, e.Location);
    }

    private void DrawMachineItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _machineList.Items.Count)
        {
            return;
        }

        var machine = (MachineEntry)_machineList.Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = Rectangle.Inflate(e.Bounds, -2, -4);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var background = new SolidBrush(machine.IsTemporary
            ? selected ? Color.FromArgb(22, 78, 99) : Color.FromArgb(19, 50, 60)
            : selected ? AppTheme.SidebarAlt : AppTheme.Sidebar);
        using var accent = new SolidBrush(machine.IsTemporary
            ? Color.FromArgb(20, 184, 166)
            : selected ? AppTheme.Accent : Color.FromArgb(71, 85, 105));
        using var title = new SolidBrush(Color.White);
        using var muted = new SolidBrush(machine.IsTemporary ? Color.FromArgb(153, 246, 228) : Color.FromArgb(148, 163, 184));
        using var titleFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);

        e.Graphics.FillRoundedRectangle(background, bounds, 8);
        e.Graphics.FillEllipse(accent, bounds.Left + 12, bounds.Top + 14, 24, 24);
        e.Graphics.DrawString(machine.DisplayName, titleFont, title, bounds.Left + 48, bounds.Top + 9);
        e.Graphics.DrawString(machine.IsTemporary ? $"Ad hoc - {machine.DnsName}" : machine.DnsName, AppTheme.SmallFont, muted, bounds.Left + 48, bounds.Top + 30);
    }

    private void LoadData()
    {
        _data = _store.Load();
    }

    private void SaveData()
    {
        _store.Save(_data);
    }

    private void RememberSession(Guid machineId)
    {
        if (_temporaryMachines.Any(machine => machine.Id == machineId))
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
        _data.AutoReconnectMachineIds = _sessions
            .Where(entry => entry.Value.IsConnected && !_temporaryMachines.Any(machine => machine.Id == entry.Key))
            .Select(entry => entry.Key)
            .Distinct()
            .ToList();
    }

    private void RestoreRememberedSessions()
    {
        if (_restoredRememberedSessions)
        {
            return;
        }

        _restoredRememberedSessions = true;
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
        var selectedId = SelectedMachine()?.Id;
        var machines = _data.Machines
            .Concat(_temporaryMachines)
            .OrderByDescending(machine => machine.IsTemporary)
            .ThenBy(machine => machine.DisplayName)
            .ToList();

        _machineList.DataSource = null;
        _machineList.DataSource = machines;
        var countText = _data.Machines.Count == 1 ? "1 Maschine" : $"{_data.Machines.Count} Maschinen";
        _machineCount.Text = _temporaryMachines.Count == 0 ? countText : $"{countText}, {_temporaryMachines.Count} ad hoc";
        if (selectedId is not null)
        {
            _machineList.SelectedItem = machines.FirstOrDefault(machine => machine.Id == selectedId);
        }
    }

    private void SelectMachine(Guid machineId)
    {
        foreach (var item in _machineList.Items)
        {
            if (item is MachineEntry machine && machine.Id == machineId)
            {
                _machineList.SelectedItem = machine;
                return;
            }
        }
    }

    private MachineEntry? SelectedMachine() => _machineList.SelectedItem as MachineEntry;

    private IRemoteSessionHost? ActiveSession()
    {
        var machine = SelectedMachine();
        return machine is not null && _sessions.TryGetValue(machine.Id, out var session) ? session : null;
    }

    private CredentialProfile? CredentialFor(MachineEntry machine)
    {
        return machine.CredentialProfileId is null
            ? null
            : _data.Credentials.FirstOrDefault(credential => credential.Id == machine.CredentialProfileId);
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

        _rdpPanel.Controls.Add(session.Control);
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
            }

            if (!_sessions.TryGetValue(machine.Id, out var session))
            {
                session = CreateSessionHost(machine, credential);
                _sessions[machine.Id] = session;
            }

            ShowSelectedSession();
            session.Connect();
            RememberSession(machine.Id);
            _statusLabel.Text = $"Verbinde mit {machine.DisplayName}";
            return true;
        }
        catch (Exception ex)
        {
            if (_sessions.TryGetValue(machine.Id, out var failedSession))
            {
                failedSession.Dispose();
                _sessions.Remove(machine.Id);
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
                    "RDP Man",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return false;
        }
    }

    private IRemoteSessionHost CreateSessionHost(MachineEntry machine, CredentialProfile? credential)
    {
        return new RdpSessionHost(machine, credential);
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
        }
    }

    private void ReconnectSelected()
    {
        var session = ActiveSession();
        if (session is null)
        {
            ConnectSelected();
            return;
        }

        try
        {
            session.Reconnect();
            _statusLabel.Text = $"Reconnect: {session.Machine.DisplayName}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Reconnect fehlgeschlagen: {session.Machine.DisplayName}";
            MessageBox.Show(
                this,
                $"Reconnect zu \"{session.Machine.DisplayName}\" ist fehlgeschlagen.\n\n{ex.Message}",
                "RDP Man",
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
        ForgetSession(machine.Id);
        RemoveTemporaryMachine(machine.Id);
        ShowSelectedSession();
        _statusLabel.Text = $"Getrennt: {machine.DisplayName}";
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

    private void ShowBackupMenu(Control owner)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Backup erstellen", null, (_, _) => ExportBackup());
        menu.Items.Add("Backup wiederherstellen", null, (_, _) => ImportBackup());
        menu.Closed += (_, _) => BeginInvoke(() => menu.Dispose());
        menu.Show(owner, new Point(0, owner.Height + 4));
    }

    private void ExportBackup()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "RDP Man Backup erstellen",
            FileName = $"rdpman-backup-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Filter = "RDP Man Backup (*.json)|*.json|Alle Dateien (*.*)|*.*",
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
            MessageBox.Show(this, "Backup wurde erstellt.", "RDP Man", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            Title = "RDP Man Backup wiederherstellen",
            Filter = "RDP Man Backup (*.json)|*.json|Alle Dateien (*.*)|*.*",
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
            MessageBox.Show(this, "Ad-hoc-Verbindungen sind nur temporÃ¤r und kÃ¶nnen nicht bearbeitet werden.", "RDP Man", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new MachineEditForm(_data, machine);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Machine is null)
        {
            return;
        }

        machine.Name = dialog.Machine.Name;
        machine.DnsName = dialog.Machine.DnsName;
        machine.Notes = dialog.Machine.Notes;
        machine.CredentialProfileId = dialog.Machine.CredentialProfileId;
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
            });
            existingDns.Add(dnsName.ToLowerInvariant());
            imported++;
        }

        SaveData();
        RefreshMachineList();
        _statusLabel.Text = $"{imported} Maschinen importiert";
    }
}
