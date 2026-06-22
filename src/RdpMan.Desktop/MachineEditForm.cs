namespace RdpMan.Desktop;

public sealed class MachineEditForm : Form
{
    private readonly TextBox _name = AppTheme.TextBox();
    private readonly TextBox _dnsName = AppTheme.TextBox();
    private readonly TextBox _groupName = AppTheme.TextBox();
    private readonly TextBox _notes = AppTheme.TextBox(multiline: true);
    private readonly ComboBox _credential = new();
    private readonly ComboBox _color = new();
    private readonly CheckBox _favorite = new();
    private readonly CheckBox _redirectClipboard = new();
    private readonly CheckBox _redirectPrinters = new();
    private readonly CheckBox _redirectSmartCards = new();
    private readonly CheckBox _redirectWebAuthn = new();
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
                GroupName = machine.GroupName,
                ColorKey = machine.ColorKey,
                Notes = machine.Notes,
                CredentialProfileId = machine.CredentialProfileId,
                IsFavorite = machine.IsFavorite,
                RedirectClipboard = machine.RedirectClipboard,
                RedirectPrinters = machine.RedirectPrinters,
                RedirectSmartCards = machine.RedirectSmartCards,
                RedirectWebAuthn = machine.RedirectWebAuthn,
            };

        Text = machine is null ? "Maschine hinzufuegen" : "Maschine bearbeiten";
        Width = 600;
        Height = 720;
        MinimumSize = new Size(560, 640);
        AppTheme.ApplyWindow(this);

        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        var card = AppTheme.Card();
        var title = new Label
        {
            Text = Text,
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var description = new Label
        {
            Text = "Name, Zieladresse, Ordnung, Standard-Zugang und RDP-Sicherheitsoptionen.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppTheme.Surface,
            Padding = new Padding(0, 10, 0, 0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 9,
            Height = 486,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _credential.DropDownStyle = ComboBoxStyle.DropDownList;
        _credential.FlatStyle = FlatStyle.Flat;
        _credential.Height = 32;
        AppTheme.StyleInput(_credential);

        _color.DropDownStyle = ComboBoxStyle.DropDownList;
        _color.FlatStyle = FlatStyle.Flat;
        _color.Height = 32;
        AppTheme.StyleInput(_color);

        StyleCheckBox(_favorite, "Favorit");
        StyleCheckBox(_redirectClipboard, "Zwischenablage erlauben");
        StyleCheckBox(_redirectPrinters, "Drucker umleiten");
        StyleCheckBox(_redirectSmartCards, "Smartcards umleiten");
        StyleCheckBox(_redirectWebAuthn, "WebAuthn / Windows Hello erlauben");

        var security = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 74,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = AppTheme.Surface,
        };
        security.Controls.Add(_redirectClipboard);
        security.Controls.Add(_redirectPrinters);
        security.Controls.Add(_redirectSmartCards);
        security.Controls.Add(_redirectWebAuthn);

        layout.Controls.Add(Field("Name", _name, "z.B. Terminalserver 01"), 0, 0);
        layout.Controls.Add(Field("DNS-Name / Host", _dnsName, "server.domain.local oder IP"), 0, 1);
        layout.Controls.Add(Field("Gruppe", _groupName, "z.B. Server, CNC, Buero"), 0, 2);
        layout.Controls.Add(Field("Farbe", _color), 0, 3);
        layout.Controls.Add(Field("Standard-Zugang", _credential), 0, 4);
        layout.Controls.Add(CheckField(_favorite), 0, 5);
        layout.Controls.Add(Field("RDP-Freigaben", security), 0, 6);
        layout.Controls.Add(Field("Notizen", _notes, "optional"), 0, 7);

        scroll.Controls.Add(layout);

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
        card.Controls.Add(scroll);
        card.Controls.Add(description);
        card.Controls.Add(title);
        Controls.Add(card);
        Controls.Add(buttons);
    }

    private void LoadValues()
    {
        _name.Text = Machine?.Name ?? "";
        _dnsName.Text = Machine?.DnsName ?? "";
        _groupName.Text = Machine?.GroupName ?? "";
        _notes.Text = Machine?.Notes ?? "";
        _favorite.Checked = Machine?.IsFavorite == true;
        _redirectClipboard.Checked = Machine?.RedirectClipboard == true;
        _redirectPrinters.Checked = Machine?.RedirectPrinters == true;
        _redirectSmartCards.Checked = Machine?.RedirectSmartCards == true;
        _redirectWebAuthn.Checked = Machine?.RedirectWebAuthn == true;

        foreach (var choice in ColorChoices.All)
        {
            _color.Items.Add(choice);
        }

        var selectedColor = string.IsNullOrWhiteSpace(Machine?.ColorKey) ? "blue" : Machine.ColorKey;
        for (var index = 0; index < _color.Items.Count; index++)
        {
            if (_color.Items[index] is ColorChoice choice && choice.Key == selectedColor)
            {
                _color.SelectedIndex = index;
                break;
            }
        }
        if (_color.SelectedIndex < 0)
        {
            _color.SelectedIndex = 0;
        }

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
        Machine.GroupName = _groupName.Text.Trim();
        Machine.ColorKey = (_color.SelectedItem as ColorChoice)?.Key ?? "blue";
        Machine.Notes = _notes.Text.Trim();
        Machine.CredentialProfileId = (_credential.SelectedItem as CredentialChoice)?.Id;
        Machine.IsFavorite = _favorite.Checked;
        Machine.RedirectClipboard = _redirectClipboard.Checked;
        Machine.RedirectPrinters = _redirectPrinters.Checked;
        Machine.RedirectSmartCards = _redirectSmartCards.Checked;
        Machine.RedirectWebAuthn = _redirectWebAuthn.Checked;
    }

    private static void StyleCheckBox(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.AutoSize = true;
        checkBox.Font = AppTheme.UiFont;
        checkBox.ForeColor = AppTheme.Text;
        checkBox.Margin = new Padding(0, 2, 18, 6);
    }

    private sealed record CredentialChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ColorChoice(string Key, string Label)
    {
        public override string ToString() => Label;
    }

    private static class ColorChoices
    {
        public static readonly ColorChoice[] All =
        [
            new("blue", "Blau"),
            new("green", "Gruen"),
            new("amber", "Gelb"),
            new("red", "Rot"),
            new("violet", "Violett"),
            new("cyan", "Cyan"),
            new("slate", "Grau"),
        ];
    }

    private static Panel Field(string label, TextBox input, string placeholder)
    {
        input.PlaceholderText = placeholder;
        return Field(label, (Control)input);
    }

    private static Panel Field(string label, Control input)
    {
        var height = input switch
        {
            TextBox { Multiline: true } => 118,
            FlowLayoutPanel => 104,
            _ => 62,
        };

        var panel = new Panel { Dock = DockStyle.Top, Height = height };
        var labelControl = AppTheme.Label(label);
        labelControl.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }

    private static Panel CheckField(CheckBox input)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 44 };
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        return panel;
    }
}
