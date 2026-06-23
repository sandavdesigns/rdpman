namespace RdpMan.Desktop;

public sealed class CredentialManagerForm : Form
{
    private readonly AppData _data;
    private readonly ListBox _list = new();
    private readonly ComboBox _globalCredential = new();

    public CredentialManagerForm(AppData data)
    {
        _data = data;
        Text = "Zugänge";
        Width = 680;
        Height = 520;
        MinimumSize = new Size(620, 460);
        AppTheme.ApplyWindow(this);

        BuildLayout();
        RefreshList();
    }

    private void BuildLayout()
    {
        var root = AppTheme.Card();
        var title = new Label
        {
            Text = "Zugänge",
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var description = new Label
        {
            Text = "Credential-Profile verwalten und Maschinen als Standard-Zugang zuweisen.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };

        _list.Dock = DockStyle.Fill;
        _list.DisplayMember = nameof(CredentialProfile.DisplayName);
        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = AppTheme.SurfaceAlt;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = 58;
        _list.IntegralHeight = false;
        _list.DrawItem += DrawCredentialItem;

        _globalCredential.DropDownStyle = ComboBoxStyle.DropDownList;
        _globalCredential.FlatStyle = FlatStyle.Flat;
        AppTheme.StyleInput(_globalCredential);
        _globalCredential.SelectedIndexChanged += GlobalCredentialChanged;

        var globalPanel = Field("Globaler Standard-Zugang", _globalCredential);

        var buttons = AppTheme.Footer();
        var close = AppTheme.Button("Fertig", primary: true);
        var remove = AppTheme.Button("Löschen");
        var edit = AppTheme.Button("Bearbeiten");
        var add = AppTheme.Button("Neu");
        close.DialogResult = DialogResult.OK;
        add.Click += (_, _) => AddCredential();
        edit.Click += (_, _) => EditCredential();
        remove.Click += (_, _) => RemoveCredential();
        buttons.Controls.Add(close);
        buttons.Controls.Add(remove);
        buttons.Controls.Add(edit);
        buttons.Controls.Add(add);

        var listWrap = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.SurfaceAlt,
            Padding = new Padding(8),
        };
        listWrap.Controls.Add(_list);

        root.Controls.Add(listWrap);
        root.Controls.Add(globalPanel);
        root.Controls.Add(description);
        root.Controls.Add(title);
        Controls.Add(root);
        Controls.Add(buttons);
        AcceptButton = close;
    }

    private void DrawCredentialItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _list.Items.Count)
        {
            return;
        }

        var credential = (CredentialProfile)_list.Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = Rectangle.Inflate(e.Bounds, -4, -5);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var background = new SolidBrush(selected ? AppTheme.AccentSoft : Color.White);
        using var marker = new SolidBrush(selected ? AppTheme.Accent : AppTheme.Success);
        using var title = new SolidBrush(AppTheme.Text);
        using var muted = new SolidBrush(AppTheme.MutedText);
        using var titleFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);

        e.Graphics.FillRoundedRectangle(background, bounds, 8);
        e.Graphics.FillEllipse(marker, bounds.Left + 14, bounds.Top + 15, 24, 24);
        e.Graphics.DrawString(credential.Label.Length == 0 ? credential.Username : credential.Label, titleFont, title, bounds.Left + 50, bounds.Top + 9);
        var user = string.IsNullOrWhiteSpace(credential.Domain) ? credential.Username : $"{credential.Domain}\\{credential.Username}";
        e.Graphics.DrawString(user, AppTheme.SmallFont, muted, bounds.Left + 50, bounds.Top + 31);
    }

    private void RefreshList()
    {
        _list.DataSource = null;
        _list.DataSource = _data.Credentials.OrderBy(credential => credential.DisplayName).ToList();
        RefreshGlobalCredential();
    }

    private void RefreshGlobalCredential()
    {
        _globalCredential.SelectedIndexChanged -= GlobalCredentialChanged;
        _globalCredential.Items.Clear();
        _globalCredential.Items.Add(new CredentialChoice(null, "Aktueller Windows-Benutzer"));
        foreach (var credential in _data.Credentials.OrderBy(credential => credential.DisplayName))
        {
            _globalCredential.Items.Add(new CredentialChoice(credential.Id, credential.DisplayName));
        }

        for (var index = 0; index < _globalCredential.Items.Count; index++)
        {
            if (_globalCredential.Items[index] is CredentialChoice choice && choice.Id == _data.GlobalCredentialProfileId)
            {
                _globalCredential.SelectedIndex = index;
                _globalCredential.SelectedIndexChanged += GlobalCredentialChanged;
                return;
            }
        }

        _globalCredential.SelectedIndex = 0;
        _data.GlobalCredentialProfileId = null;
        _globalCredential.SelectedIndexChanged += GlobalCredentialChanged;
    }

    private void GlobalCredentialChanged(object? sender, EventArgs e)
    {
        if (_globalCredential.SelectedItem is CredentialChoice choice)
        {
            _data.GlobalCredentialProfileId = choice.Id;
        }
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

        if (MessageBox.Show(this, "Zugang wirklich löschen?", Brand.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _data.Credentials.RemoveAll(item => item.Id == credential.Id);
        if (_data.GlobalCredentialProfileId == credential.Id)
        {
            _data.GlobalCredentialProfileId = null;
        }
        foreach (var machine in _data.Machines.Where(machine => machine.CredentialProfileId == credential.Id))
        {
            machine.CredentialProfileId = null;
        }
        foreach (var group in _data.Groups.Where(group => group.CredentialProfileId == credential.Id))
        {
            group.CredentialProfileId = null;
        }
        RefreshList();
    }

    private sealed record CredentialChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }

    private static Panel Field(string label, Control input)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(0, 8, 0, 0),
        };
        var labelControl = AppTheme.Label(label);
        labelControl.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }
}
