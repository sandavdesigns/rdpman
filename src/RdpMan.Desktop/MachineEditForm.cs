namespace RdpMan.Desktop;

public sealed class MachineEditForm : Form
{
    private readonly TextBox _name = AppTheme.TextBox();
    private readonly TextBox _dnsName = AppTheme.TextBox();
    private readonly TextBox _notes = AppTheme.TextBox(multiline: true);
    private readonly ComboBox _group = new();
    private readonly ComboBox _credential = new();
    private readonly ComboBox _color = new();
    private readonly CheckBox _favorite = new();
    private readonly CheckBox _useGlobalRedirectSettings = new();
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
            ? new MachineEntry { UseGlobalRedirectSettings = true }
            : new MachineEntry
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
                UseGlobalRedirectSettings = machine.UseGlobalRedirectSettings ?? true,
                RedirectClipboard = machine.RedirectClipboard,
                RedirectPrinters = machine.RedirectPrinters,
                RedirectSmartCards = machine.RedirectSmartCards,
                RedirectWebAuthn = machine.RedirectWebAuthn,
            };

        Text = machine is null ? "Maschine hinzufuegen" : "Maschine bearbeiten";
        Width = 600;
        Height = 760;
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
            RowCount = 10,
            Height = 548,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _group.DropDownStyle = ComboBoxStyle.DropDownList;
        _group.FlatStyle = FlatStyle.Flat;
        AppTheme.StyleInput(_group);

        _credential.DropDownStyle = ComboBoxStyle.DropDownList;
        _credential.FlatStyle = FlatStyle.Flat;
        AppTheme.StyleInput(_credential);

        ColorPalette.ConfigureColorCombo(_color);

        StyleCheckBox(_favorite, "Favorit");
        StyleCheckBox(_useGlobalRedirectSettings, "Globale Freigaben verwenden");
        StyleCheckBox(_redirectClipboard, "Zwischenablage erlauben");
        StyleCheckBox(_redirectPrinters, "Drucker umleiten");
        StyleCheckBox(_redirectSmartCards, "Smartcards umleiten");
        StyleCheckBox(_redirectWebAuthn, "WebAuthn / Windows Hello erlauben");
        _useGlobalRedirectSettings.CheckedChanged += (_, _) => UpdateRedirectControls();

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
        layout.Controls.Add(Field("Gruppe", _group), 0, 2);
        layout.Controls.Add(Field("Farbe", _color), 0, 3);
        layout.Controls.Add(Field("Standard-Zugang", _credential), 0, 4);
        layout.Controls.Add(CheckField(_favorite), 0, 5);
        layout.Controls.Add(CheckField(_useGlobalRedirectSettings), 0, 6);
        layout.Controls.Add(Field("RDP-Freigaben", security), 0, 7);
        layout.Controls.Add(Field("Notizen", _notes, "optional"), 0, 8);

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
        _notes.Text = Machine?.Notes ?? "";
        _favorite.Checked = Machine?.IsFavorite == true;
        _useGlobalRedirectSettings.Checked = Machine?.UseGlobalRedirectSettings != false;
        if (_useGlobalRedirectSettings.Checked)
        {
            ApplyGlobalRedirectValues();
        }
        else
        {
            _redirectClipboard.Checked = Machine?.RedirectClipboard == true;
            _redirectPrinters.Checked = Machine?.RedirectPrinters == true;
            _redirectSmartCards.Checked = Machine?.RedirectSmartCards == true;
            _redirectWebAuthn.Checked = Machine?.RedirectWebAuthn == true;
        }
        UpdateRedirectControls();

        _group.Items.Add(new GroupChoice(null, "Keine Gruppe"));
        foreach (var group in _data.Groups.OrderBy(item => item.DisplayName))
        {
            _group.Items.Add(new GroupChoice(group.Id, group.DisplayName));
        }

        var selectedGroupId = Machine?.GroupId;
        if (selectedGroupId is null && !string.IsNullOrWhiteSpace(Machine?.GroupName))
        {
            selectedGroupId = _data.Groups.FirstOrDefault(group => group.Name.Equals(Machine.GroupName, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        for (var index = 0; index < _group.Items.Count; index++)
        {
            if (_group.Items[index] is GroupChoice choice && choice.Id == selectedGroupId)
            {
                _group.SelectedIndex = index;
                break;
            }
        }
        if (_group.SelectedIndex < 0)
        {
            _group.SelectedIndex = 0;
        }

        _color.Items.Add(new ColorChoice("", "Gruppenfarbe verwenden"));
        foreach (var choice in ColorPalette.Choices)
        {
            _color.Items.Add(choice);
        }

        var selectedColor = Machine?.ColorKey ?? "";
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

        _credential.Items.Add(new CredentialChoice(null, "Gruppe/global verwenden"));
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
        Machine.GroupId = (_group.SelectedItem as GroupChoice)?.Id;
        Machine.GroupName = Machine.GroupId is null
            ? ""
            : _data.Groups.FirstOrDefault(group => group.Id == Machine.GroupId)?.Name ?? "";
        Machine.ColorKey = (_color.SelectedItem as ColorChoice)?.Key ?? "";
        Machine.Notes = _notes.Text.Trim();
        Machine.CredentialProfileId = (_credential.SelectedItem as CredentialChoice)?.Id;
        Machine.IsFavorite = _favorite.Checked;
        Machine.UseGlobalRedirectSettings = _useGlobalRedirectSettings.Checked;
        Machine.RedirectClipboard = _redirectClipboard.Checked;
        Machine.RedirectPrinters = _redirectPrinters.Checked;
        Machine.RedirectSmartCards = _redirectSmartCards.Checked;
        Machine.RedirectWebAuthn = _redirectWebAuthn.Checked;
    }

    private void UpdateRedirectControls()
    {
        if (_useGlobalRedirectSettings.Checked)
        {
            ApplyGlobalRedirectValues();
        }

        _redirectClipboard.Enabled = !_useGlobalRedirectSettings.Checked;
        _redirectPrinters.Enabled = !_useGlobalRedirectSettings.Checked;
        _redirectSmartCards.Enabled = !_useGlobalRedirectSettings.Checked;
        _redirectWebAuthn.Enabled = !_useGlobalRedirectSettings.Checked;
    }

    private void ApplyGlobalRedirectValues()
    {
        _redirectClipboard.Checked = _data.GlobalRedirectClipboard;
        _redirectPrinters.Checked = _data.GlobalRedirectPrinters;
        _redirectSmartCards.Checked = _data.GlobalRedirectSmartCards;
        _redirectWebAuthn.Checked = _data.GlobalRedirectWebAuthn;
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

    private sealed record GroupChoice(Guid? Id, string Label)
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
