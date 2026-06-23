namespace RdpMan.Desktop;

public sealed class ConnectAsForm : Form
{
    private readonly AppData _data;
    private readonly bool _allowRememberDefault;
    private readonly bool _showHost;
    private readonly TextBox _host = AppTheme.TextBox();
    private readonly ComboBox _credential = new();
    private readonly TextBox _domain = AppTheme.TextBox();
    private readonly TextBox _username = AppTheme.TextBox();
    private readonly TextBox _password = AppTheme.TextBox();
    private readonly CheckBox _rememberDefault = new();
    private readonly TableLayoutPanel _manualFields = new();

    public string HostName => _host.Text.Trim();
    public CredentialProfile? SelectedCredential { get; private set; }
    public Guid? SelectedCredentialProfileId { get; private set; }
    public bool RememberAsQuickConnectDefault => _allowRememberDefault && _rememberDefault.Checked;

    public ConnectAsForm(AppData data, string title, bool allowRememberDefault, bool showHost = false)
    {
        _data = data;
        _allowRememberDefault = allowRememberDefault;
        _showHost = showHost;

        Text = title;
        Width = 520;
        Height = (allowRememberDefault ? 500 : 460) + (showHost ? 62 : 0);
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
            Text = "Gespeicherten Zugang nutzen oder einmalige Anmeldedaten eingeben.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };

        _credential.DropDownStyle = ComboBoxStyle.DropDownList;
        _credential.FlatStyle = FlatStyle.Flat;
        _credential.Height = 32;
        _credential.SelectedIndexChanged += (_, _) => UpdateManualFields();
        AppTheme.StyleInput(_credential);

        _password.UseSystemPasswordChar = true;

        _manualFields.Dock = DockStyle.Top;
        _manualFields.ColumnCount = 1;
        _manualFields.RowCount = 3;
        _manualFields.Height = 198;
        _manualFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _manualFields.Controls.Add(Field("Domain", _domain, "optional"), 0, 0);
        _manualFields.Controls.Add(Field("Benutzer", _username, "Benutzername"), 0, 1);
        _manualFields.Controls.Add(Field("Passwort", _password, "Passwort"), 0, 2);

        _rememberDefault.Text = "Als Quick-Connect-Standard merken";
        _rememberDefault.Dock = DockStyle.Top;
        _rememberDefault.Height = 30;
        _rememberDefault.Font = AppTheme.UiFont;
        _rememberDefault.ForeColor = AppTheme.Text;
        _rememberDefault.Visible = _allowRememberDefault;

        var layout = new Panel
        {
            Dock = DockStyle.Top,
            Height = (_allowRememberDefault ? 300 : 260) + (_showHost ? 62 : 0),
            Padding = new Padding(0, 12, 0, 0),
        };
        layout.Controls.Add(_manualFields);
        layout.Controls.Add(_rememberDefault);
        layout.Controls.Add(Field("Zugang", _credential));
        if (_showHost)
        {
            layout.Controls.Add(Field("PC-Name / IP", _host, "PC-Name oder IP"));
        }

        var buttons = AppTheme.Footer();
        var connect = AppTheme.Button("Verbinden", primary: true);
        var cancel = AppTheme.Button("Abbrechen");
        connect.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        connect.Click += (_, _) => Save();
        buttons.Controls.Add(connect);
        buttons.Controls.Add(cancel);

        AcceptButton = connect;
        CancelButton = cancel;
        card.Controls.Add(layout);
        card.Controls.Add(description);
        card.Controls.Add(title);
        Controls.Add(card);
        Controls.Add(buttons);
    }

    private void LoadValues()
    {
        _credential.Items.Add(new CredentialChoice(null, "Aktueller Windows-Benutzer", false));
        foreach (var credential in _data.Credentials.OrderBy(item => item.DisplayName))
        {
            _credential.Items.Add(new CredentialChoice(credential.Id, credential.DisplayName, false));
        }
        _credential.Items.Add(new CredentialChoice(null, "Andere Zugangsdaten eingeben", true));

        for (var index = 0; index < _credential.Items.Count; index++)
        {
            if (_credential.Items[index] is CredentialChoice choice && choice.Id == _data.QuickConnectCredentialProfileId)
            {
                _credential.SelectedIndex = index;
                return;
            }
        }

        _credential.SelectedIndex = 0;
    }

    private void UpdateManualFields()
    {
        var manual = (_credential.SelectedItem as CredentialChoice)?.Manual == true;
        _manualFields.Visible = manual;
        _rememberDefault.Enabled = !manual && (_credential.SelectedItem as CredentialChoice)?.Id is not null;
    }

    private void Save()
    {
        if (_showHost && string.IsNullOrWhiteSpace(_host.Text))
        {
            MessageBox.Show(this, "PC-Name oder IP ist erforderlich.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var choice = _credential.SelectedItem as CredentialChoice;
        if (choice?.Manual == true)
        {
            if (string.IsNullOrWhiteSpace(_username.Text))
            {
                MessageBox.Show(this, "Benutzername ist erforderlich.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            SelectedCredential = new CredentialProfile
            {
                Label = "Einmalig",
                Domain = _domain.Text.Trim(),
                Username = _username.Text.Trim(),
                ProtectedPassword = string.IsNullOrWhiteSpace(_password.Text) ? "" : CredentialVault.Protect(_password.Text),
            };
            SelectedCredentialProfileId = null;
            return;
        }

        SelectedCredentialProfileId = choice?.Id;
        SelectedCredential = choice?.Id is null
            ? null
            : _data.Credentials.FirstOrDefault(credential => credential.Id == choice.Id);
    }

    private static Panel Field(string label, TextBox input, string placeholder)
    {
        input.PlaceholderText = placeholder;
        return Field(label, (Control)input);
    }

    private static Panel Field(string label, Control input)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 62 };
        var labelControl = AppTheme.Label(label);
        labelControl.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }

    private sealed record CredentialChoice(Guid? Id, string Label, bool Manual)
    {
        public override string ToString() => Label;
    }
}
