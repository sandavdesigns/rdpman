namespace RdpMan.Desktop;

public sealed class CredentialManagerForm : Form
{
    private readonly AppData _data;
    private readonly ListBox _list = new();

    public CredentialManagerForm(AppData data)
    {
        _data = data;
        Text = "Zugänge";
        Width = 580;
        Height = 420;
        StartPosition = FormStartPosition.CenterParent;

        BuildLayout();
        RefreshList();
    }

    private void BuildLayout()
    {
        _list.Dock = DockStyle.Fill;
        _list.DisplayMember = nameof(CredentialProfile.DisplayName);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };

        var close = new Button { Text = "Fertig", DialogResult = DialogResult.OK };
        var remove = new Button { Text = "Löschen" };
        var edit = new Button { Text = "Bearbeiten" };
        var add = new Button { Text = "Neu" };
        add.Click += (_, _) => AddCredential();
        edit.Click += (_, _) => EditCredential();
        remove.Click += (_, _) => RemoveCredential();
        buttons.Controls.Add(close);
        buttons.Controls.Add(remove);
        buttons.Controls.Add(edit);
        buttons.Controls.Add(add);

        Controls.Add(_list);
        Controls.Add(buttons);
        AcceptButton = close;
    }

    private void RefreshList()
    {
        _list.DataSource = null;
        _list.DataSource = _data.Credentials.OrderBy(credential => credential.DisplayName).ToList();
    }

    private CredentialProfile? SelectedCredential() => _list.SelectedItem as CredentialProfile;

    private void AddCredential()
    {
        using var dialog = new CredentialEditForm(null);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Credential is null)
        {
            return;
        }
        _data.Credentials.Add(dialog.Credential);
        RefreshList();
    }

    private void EditCredential()
    {
        var credential = SelectedCredential();
        if (credential is null)
        {
            return;
        }

        using var dialog = new CredentialEditForm(credential);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Credential is null)
        {
            return;
        }

        credential.Label = dialog.Credential.Label;
        credential.Domain = dialog.Credential.Domain;
        credential.Username = dialog.Credential.Username;
        credential.ProtectedPassword = dialog.Credential.ProtectedPassword;
        RefreshList();
    }

    private void RemoveCredential()
    {
        var credential = SelectedCredential();
        if (credential is null)
        {
            return;
        }

        if (MessageBox.Show(this, "Zugang wirklich löschen?", "RDP Man", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _data.Credentials.RemoveAll(item => item.Id == credential.Id);
        foreach (var machine in _data.Machines.Where(machine => machine.CredentialProfileId == credential.Id))
        {
            machine.CredentialProfileId = null;
        }
        RefreshList();
    }
}

