namespace RdpMan.Desktop;

public sealed class CredentialEditForm : Form
{
    private readonly TextBox _label = new();
    private readonly TextBox _domain = new();
    private readonly TextBox _username = new();
    private readonly TextBox _password = new();
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
        Width = 430;
        Height = 260;
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

        _password.UseSystemPasswordChar = true;

        layout.Controls.Add(new Label { Text = "Label", AutoSize = true }, 0, 0);
        layout.Controls.Add(_label, 1, 0);
        layout.Controls.Add(new Label { Text = "Domain", AutoSize = true }, 0, 1);
        layout.Controls.Add(_domain, 1, 1);
        layout.Controls.Add(new Label { Text = "Benutzer", AutoSize = true }, 0, 2);
        layout.Controls.Add(_username, 1, 2);
        layout.Controls.Add(new Label { Text = "Passwort", AutoSize = true }, 0, 3);
        layout.Controls.Add(_password, 1, 3);

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
        _label.Text = Credential?.Label ?? "";
        _domain.Text = Credential?.Domain ?? "";
        _username.Text = Credential?.Username ?? "";
        _password.PlaceholderText = _editingExisting ? "Leer lassen, um Passwort beizubehalten" : "";
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_username.Text))
        {
            MessageBox.Show(this, "Benutzername ist erforderlich.", "RDP Man", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (!_editingExisting && string.IsNullOrWhiteSpace(_password.Text))
        {
            MessageBox.Show(this, "Passwort ist erforderlich.", "RDP Man", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

