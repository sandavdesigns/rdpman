namespace RdpMan.Desktop;

public sealed class GroupManagerForm : Form
{
    private readonly AppData _data;
    private readonly ListBox _list = new();

    public GroupManagerForm(AppData data)
    {
        _data = data;
        Text = "Gruppen";
        Width = 700;
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
            Text = "Gruppen",
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var description = new Label
        {
            Text = "Zentrale Gruppen mit Farbe und optionalem Standard-Zugang verwalten.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };

        _list.Dock = DockStyle.Fill;
        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = AppTheme.SurfaceAlt;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = 58;
        _list.IntegralHeight = false;
        _list.DrawItem += DrawGroupItem;
        _list.DoubleClick += (_, _) => EditGroup();

        var buttons = AppTheme.Footer();
        var close = AppTheme.Button("Fertig", primary: true);
        var remove = AppTheme.Button("Entfernen");
        var edit = AppTheme.Button("Bearbeiten");
        var add = AppTheme.Button("Neu");
        close.DialogResult = DialogResult.OK;
        add.Click += (_, _) => AddGroup();
        edit.Click += (_, _) => EditGroup();
        remove.Click += (_, _) => RemoveGroup();
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
        root.Controls.Add(description);
        root.Controls.Add(title);
        Controls.Add(root);
        Controls.Add(buttons);
        AcceptButton = close;
    }

    private void DrawGroupItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _list.Items.Count)
        {
            return;
        }

        var group = (MachineGroup)_list.Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = Rectangle.Inflate(e.Bounds, -4, -5);
        var credential = group.CredentialProfileId is null
            ? "Globaler Zugang"
            : _data.Credentials.FirstOrDefault(item => item.Id == group.CredentialProfileId)?.DisplayName ?? "Zugang fehlt";
        var count = _data.Machines.Count(machine => machine.GroupId == group.Id);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var background = new SolidBrush(selected ? AppTheme.AccentSoft : Color.White);
        using var marker = new SolidBrush(ColorPalette.Marker(group.ColorKey, isConnected: true));
        using var title = new SolidBrush(AppTheme.Text);
        using var muted = new SolidBrush(AppTheme.MutedText);
        using var titleFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);

        e.Graphics.FillRoundedRectangle(background, bounds, 8);
        e.Graphics.FillEllipse(marker, bounds.Left + 14, bounds.Top + 15, 24, 24);
        e.Graphics.DrawString(group.DisplayName, titleFont, title, bounds.Left + 50, bounds.Top + 9);
        e.Graphics.DrawString($"{credential} - {count} Rechner", AppTheme.SmallFont, muted, bounds.Left + 50, bounds.Top + 31);
    }

    private void RefreshList()
    {
        _list.DataSource = null;
        _list.DataSource = _data.Groups.OrderBy(group => group.DisplayName).ToList();
    }

    private MachineGroup? SelectedGroup() => _list.SelectedItem as MachineGroup;

    private void AddGroup()
    {
        using var dialog = new GroupEditForm(_data, null);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Group is null)
        {
            return;
        }

        _data.Groups.Add(dialog.Group);
        RefreshList();
    }

    private void EditGroup()
    {
        var group = SelectedGroup();
        if (group is null)
        {
            return;
        }

        using var dialog = new GroupEditForm(_data, group);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Group is null)
        {
            return;
        }

        group.Name = dialog.Group.Name;
        group.ColorKey = dialog.Group.ColorKey;
        group.CredentialProfileId = dialog.Group.CredentialProfileId;
        RefreshList();
    }

    private void RemoveGroup()
    {
        var group = SelectedGroup();
        if (group is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Gruppe \"{group.DisplayName}\" entfernen?\n\nRechner in dieser Gruppe bleiben erhalten und werden auf keine Gruppe gesetzt.",
            "Gruppe entfernen",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            return;
        }

        foreach (var machine in _data.Machines.Where(machine => machine.GroupId == group.Id))
        {
            machine.GroupId = null;
            machine.GroupName = "";
        }
        _data.Groups.RemoveAll(item => item.Id == group.Id);
        RefreshList();
    }
}
