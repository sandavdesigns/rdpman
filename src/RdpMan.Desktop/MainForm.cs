using System.Diagnostics;

namespace RdpMan.Desktop;

public sealed class MainForm : Form
{
    private readonly DataStore _store = new();
    private readonly Dictionary<Guid, RdpSessionHost> _sessions = [];
    private AppData _data = new();

    private readonly SplitContainer _split = new();
    private readonly ListBox _machineList = new();
    private readonly Panel _rdpPanel = new();
    private readonly Label _placeholder = new();
    private readonly ToolStrip _toolStrip = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    public MainForm()
    {
        Text = "RDP Man";
        MinimumSize = new Size(1100, 720);
        Width = 1300;
        Height = 820;

        BuildLayout();
        LoadData();
        RefreshMachineList();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        _toolStrip.Items.Add(Button("Neu", AddMachine));
        _toolStrip.Items.Add(Button("Bearbeiten", EditMachine));
        _toolStrip.Items.Add(Button("Zugänge", ManageCredentials));
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(Button("AD Import", ImportFromAd));
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(Button("Connect", ConnectSelected));
        _toolStrip.Items.Add(Button("Reconnect", ReconnectSelected));
        _toolStrip.Items.Add(Button("Disconnect", DisconnectSelected));
        _toolStrip.Items.Add(Button("Ping", PingSelected));

        _split.Dock = DockStyle.Fill;
        _split.SplitterDistance = 300;
        _split.FixedPanel = FixedPanel.Panel1;

        _machineList.Dock = DockStyle.Fill;
        _machineList.DisplayMember = nameof(MachineEntry.DisplayName);
        _machineList.SelectedIndexChanged += (_, _) => ShowSelectedSession();
        _machineList.DoubleClick += (_, _) => ConnectSelected();

        _rdpPanel.Dock = DockStyle.Fill;
        _rdpPanel.BackColor = Color.FromArgb(24, 28, 34);
        _rdpPanel.Resize += (_, _) => ActiveSession()?.ResizeToHost();

        _placeholder.Dock = DockStyle.Fill;
        _placeholder.Text = "Links eine Maschine auswählen und verbinden.";
        _placeholder.TextAlign = ContentAlignment.MiddleCenter;
        _placeholder.ForeColor = Color.WhiteSmoke;
        _placeholder.Font = new Font(Font.FontFamily, 14, FontStyle.Regular);
        _rdpPanel.Controls.Add(_placeholder);

        _split.Panel1.Controls.Add(_machineList);
        _split.Panel2.Controls.Add(_rdpPanel);

        _statusStrip.Items.Add(_statusLabel);
        _statusLabel.Text = "Bereit";

        Controls.Add(_split);
        Controls.Add(_toolStrip);
        Controls.Add(_statusStrip);
        _toolStrip.Dock = DockStyle.Top;
        _statusStrip.Dock = DockStyle.Bottom;
    }

    private static ToolStripButton Button(string text, Action action)
    {
        var button = new ToolStripButton(text);
        button.Click += (_, _) => action();
        return button;
    }

    private void LoadData()
    {
        _data = _store.Load();
    }

    private void SaveData()
    {
        _store.Save(_data);
    }

    private void RefreshMachineList()
    {
        var selectedId = SelectedMachine()?.Id;
        _machineList.DataSource = null;
        _machineList.DataSource = _data.Machines.OrderBy(machine => machine.DisplayName).ToList();
        if (selectedId is not null)
        {
            _machineList.SelectedItem = _data.Machines.FirstOrDefault(machine => machine.Id == selectedId);
        }
    }

    private MachineEntry? SelectedMachine() => _machineList.SelectedItem as MachineEntry;

    private RdpSessionHost? ActiveSession()
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

        if (!_sessions.TryGetValue(machine.Id, out var session))
        {
            session = new RdpSessionHost(machine, CredentialFor(machine));
            _sessions[machine.Id] = session;
        }

        ShowSelectedSession();
        session.Connect();
        _statusLabel.Text = $"Verbinde mit {machine.DisplayName}";
    }

    private void ReconnectSelected()
    {
        var session = ActiveSession();
        if (session is null)
        {
            ConnectSelected();
            return;
        }

        session.Reconnect();
        _statusLabel.Text = $"Reconnect: {session.Machine.DisplayName}";
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
            Arguments = $"/k ping {machine.DnsName}",
            UseShellExecute = true,
        };
        Process.Start(process);
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
        }

        SaveData();
        RefreshMachineList();
        _statusLabel.Text = $"{dialog.ImportedDnsNames.Count} Maschinen aus AD importiert";
    }
}

