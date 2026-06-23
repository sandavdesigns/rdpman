namespace RdpMan.Desktop;

public sealed class CredentialEditForm : Form
{
    private readonly TextBox _label = AppTheme.TextBox();
    private readonly TextBox _domain = AppTheme.TextBox();
    private readonly TextBox _username = AppTheme.TextBox();
    private readonly TextBox _password = AppTheme.TextBox();
    private readonly bool _editingExisting;

    public CredentialProfile? Credential { get; private set; }

    public CredentialEditForm(CredentialProfile? credential)
    {
        _editingExisting = credential is not null;
        Credential = credential is null
            ? new CredentialProfile()
            : new CredentialProfile
            {
                Id = credential.Id,
                Label = credential.Label,
                Domain = credential.Domain,
                Username = credential.Username,
                ProtectedPassword = credential.ProtectedPassword,
            };

        Text = credential is null ? "Zugang hinzufügen" : "Zugang bearbeiten";
        Width = 520;
        Height = 460;
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
            Text = _editingExisting ? "Zugang bearbeiten" : "Neuen Zugang anlegen",
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var description = new Label
        {
            Text = "Anmeldedaten werden lokal mit Windows DPAPI geschützt.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            Height = 286,
            Padding = new Padding(0, 12, 0, 0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _password.UseSystemPasswordChar = true;

        layout.Controls.Add(Field("Label", _label, "z.B. Admin, Wartung, Kunde"), 0, 0);
        layout.Controls.Add(Field("Domain", _domain, "optional"), 0, 1);
        layout.Controls.Add(Field("Benutzer", _username, "Benutzername"), 0, 2);
        layout.Controls.Add(Field("Passwort", _password, _editingExisting ? "Neues Passwort eingeben oder leer lassen" : "Passwort"), 0, 3);

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
        _label.Text = Credential?.Label ?? "";
        _domain.Text = Credential?.Domain ?? "";
        _username.Text = Credential?.Username ?? "";
        _password.PlaceholderText = _editingExisting ? "Neues Passwort eingeben oder leer lassen" : "";
    }

    private static Panel Field(string label, TextBox input, string placeholder)
    {
        input.PlaceholderText = placeholder;
        var panel = new Panel { Dock = DockStyle.Top, Height = 66 };
        var labelControl = AppTheme.Label(label);
        labelControl.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_username.Text))
        {
            MessageBox.Show(this, "Benutzername ist erforderlich.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!_editingExisting && string.IsNullOrWhiteSpace(_password.Text))
        {
            MessageBox.Show(this, "Passwort ist erforderlich.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Credential ??= new CredentialProfile();
        Credential.Label = _label.Text.Trim();
        Credential.Domain = _domain.Text.Trim();
        Credential.Username = _username.Text.Trim();
        if (!string.IsNullOrWhiteSpace(_password.Text))
        {
            Credential.ProtectedPassword = CredentialVault.Protect(_password.Text);
        }
    }
}
