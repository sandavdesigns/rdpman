namespace RdpMan.Desktop;

public sealed class MachineEditForm : Form
{
    private readonly TextBox _name = AppTheme.TextBox();
    private readonly TextBox _dnsName = AppTheme.TextBox();
    private readonly TextBox _notes = AppTheme.TextBox(multiline: true);
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
        Width = 560;
        Height = 470;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AppTheme.ApplyWindow(this);

        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        var card = AppTheme.Card();
        var title = new Label
        {
            Text = Machine?.Id == Guid.Empty ? "Maschine hinzufügen" : Text,
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var description = new Label
        {
            Text = "Name, Zieladresse und optionaler Standard-Zugang für diese RDP-Session.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            Height = 300,
            Padding = new Padding(0, 12, 0, 0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _credential.DropDownStyle = ComboBoxStyle.DropDownList;
        _credential.FlatStyle = FlatStyle.Flat;
        _credential.Height = 32;
        AppTheme.StyleInput(_credential);

        layout.Controls.Add(Field("Name", _name, "z.B. Terminalserver 01"), 0, 0);
        layout.Controls.Add(Field("DNS-Name / Host", _dnsName, "server.domain.local oder IP"), 0, 1);
        layout.Controls.Add(Field("Standard-Zugang", _credential), 0, 2);
        layout.Controls.Add(Field("Notizen", _notes, "optional"), 0, 3);

        var buttons = AppTheme.Footer();
        var save = AppTheme.Button("Speichern", primary: true);
        var cancel = AppTheme.Button("Abbrechen");
        save.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        AcceptButton = save;
        CancelButton = cancel;
        card.Controls.Add(layout);
        card.Controls.Add(description);
        card.Controls.Add(title);
        Controls.Add(card);
        Controls.Add(buttons);
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

    private static Panel Field(string label, TextBox input, string placeholder)
    {
        input.PlaceholderText = placeholder;
        return Field(label, (Control)input);
    }

    private static Panel Field(string label, Control input)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = input is TextBox { Multiline: true } ? 118 : 62 };
        var labelControl = AppTheme.Label(label);
        labelControl.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }
}
