namespace RdpMan.Desktop;

public sealed class GroupEditForm : Form
{
    private readonly AppData _data;
    private readonly TextBox _name = AppTheme.TextBox();
    private readonly ComboBox _color = new();
    private readonly ComboBox _credential = new();

    public MachineGroup? Group { get; private set; }

    public GroupEditForm(AppData data, MachineGroup? group)
    {
        _data = data;
        Group = group is null
            ? new MachineGroup()
            : new MachineGroup
            {
                Id = group.Id,
                Name = group.Name,
                ColorKey = group.ColorKey,
                CredentialProfileId = group.CredentialProfileId,
            };

        Text = group is null ? "Gruppe hinzufuegen" : "Gruppe bearbeiten";
        Width = 520;
        Height = 430;
        MinimumSize = new Size(520, 410);
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
            Text = Text,
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var description = new Label
        {
            Text = "Gruppenfarbe und optionalen Standard-Zugang festlegen.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            Height = 238,
            Padding = new Padding(0, 14, 0, 10),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        ColorPalette.ConfigureColorCombo(_color);
        _credential.DropDownStyle = ComboBoxStyle.DropDownList;
        _credential.FlatStyle = FlatStyle.Flat;
        AppTheme.StyleInput(_credential);

        layout.Controls.Add(Field("Name", _name, "z.B. Server, CNC, Buero"), 0, 0);
        layout.Controls.Add(Field("Farbe", _color), 0, 1);
        layout.Controls.Add(Field("Standard-Zugang", _credential), 0, 2);

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
        _name.Text = Group?.Name ?? "";

        foreach (var choice in ColorPalette.Choices)
        {
            _color.Items.Add(choice);
        }
        var selectedColor = ColorPalette.Find(Group?.ColorKey) ?? ColorPalette.DefaultChoice;
        _color.SelectedItem = _color.Items.Cast<ColorChoice>().FirstOrDefault(choice => choice.Key == selectedColor.Key);

        _credential.Items.Add(new CredentialChoice(null, "Globalen Zugang verwenden"));
        foreach (var credential in _data.Credentials.OrderBy(item => item.DisplayName))
        {
            _credential.Items.Add(new CredentialChoice(credential.Id, credential.DisplayName));
        }

        for (var index = 0; index < _credential.Items.Count; index++)
        {
            if (_credential.Items[index] is CredentialChoice choice && choice.Id == Group?.CredentialProfileId)
            {
                _credential.SelectedIndex = index;
                return;
            }
        }
        _credential.SelectedIndex = 0;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(this, "Name ist erforderlich.", "RDP Man", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Group ??= new MachineGroup();
        Group.Name = _name.Text.Trim();
        Group.ColorKey = (_color.SelectedItem as ColorChoice)?.Key ?? "blue";
        Group.CredentialProfileId = (_credential.SelectedItem as CredentialChoice)?.Id;
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
        var panel = new Panel { Dock = DockStyle.Top, Height = 72 };
        var labelControl = AppTheme.Label(label);
        labelControl.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }
}
