namespace RdpMan.Desktop;

public sealed class MachineEditForm : Form
{
    private readonly TextBox _name = new();
    private readonly TextBox _dnsName = new();
    private readonly TextBox _notes = new();
    private readonly ComboBox _credential = new();
    private readonly AppData _data;

    public MachineEntry? Machine { get; private set; }

    public MachineEditForm(AppData data, MachineEntry? machine)
    {
        _data = data;
        Machine = machine is null
            ? new MachineEntry()
            : new MachineEntry
            {
                Id = machine.Id,
                Name = machine.Name,
                DnsName = machine.DnsName,
                Notes = machine.Notes,
                CredentialProfileId = machine.CredentialProfileId,
            };

        Text = machine is null ? "Maschine hinzufügen" : "Maschine bearbeiten";
        Width = 460;
        Height = 330;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(14),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _notes.Multiline = true;
        _notes.Height = 70;
        _credential.DropDownStyle = ComboBoxStyle.DropDownList;

        layout.Controls.Add(new Label { Text = "Name", AutoSize = true }, 0, 0);
        layout.Controls.Add(_name, 1, 0);
        layout.Controls.Add(new Label { Text = "DNS-Name", AutoSize = true }, 0, 1);
        layout.Controls.Add(_dnsName, 1, 1);
        layout.Controls.Add(new Label { Text = "Zugang", AutoSize = true }, 0, 2);
        layout.Controls.Add(_credential, 1, 2);
        layout.Controls.Add(new Label { Text = "Notizen", AutoSize = true }, 0, 3);
        layout.Controls.Add(_notes, 1, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var save = new Button { Text = "Speichern", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 1, 4);

        AcceptButton = save;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    private void LoadValues()
    {
        _name.Text = Machine?.Name ?? "";
        _dnsName.Text = Machine?.DnsName ?? "";
        _notes.Text = Machine?.Notes ?? "";

        _credential.Items.Add(new CredentialChoice(null, "Kein gespeicherter Zugang"));
        foreach (var credential in _data.Credentials.OrderBy(item => item.DisplayName))
        {
            _credential.Items.Add(new CredentialChoice(credential.Id, credential.DisplayName));
        }

        for (var index = 0; index < _credential.Items.Count; index++)
        {
            if (_credential.Items[index] is CredentialChoice choice && choice.Id == Machine?.CredentialProfileId)
            {
                _credential.SelectedIndex = index;
                return;
            }
        }
        _credential.SelectedIndex = 0;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_dnsName.Text))
        {
            MessageBox.Show(this, "DNS-Name ist erforderlich.", "RDP Man", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Machine ??= new MachineEntry();
        Machine.Name = string.IsNullOrWhiteSpace(_name.Text) ? _dnsName.Text.Trim() : _name.Text.Trim();
        Machine.DnsName = _dnsName.Text.Trim();
        Machine.Notes = _notes.Text.Trim();
        Machine.CredentialProfileId = (_credential.SelectedItem as CredentialChoice)?.Id;
    }

    private sealed record CredentialChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }
}

