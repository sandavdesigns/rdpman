using System.DirectoryServices;
using Microsoft.VisualBasic.FileIO;

namespace RdpMan.Desktop;

public sealed class SetupForm : Form
{
    private readonly AppData _data;
    private readonly Action _exportBackup;
    private readonly Action _importBackup;
    private readonly ListBox _credentials = new();
    private readonly ListBox _groups = new();
    private readonly ComboBox _globalCredential = new();
    private readonly TextBox _ldapPath = AppTheme.TextBox();
    private readonly TextBox _ldapFilter = AppTheme.TextBox();
    private readonly ComboBox _importGroup = new();
    private readonly ComboBox _importCredential = new();
    private readonly ListBox _adPreview = new();
    private readonly List<string> _previewNames = [];

    public bool DataChanged { get; private set; }

    public SetupForm(AppData data, Action exportBackup, Action importBackup)
    {
        _data = data;
        _exportBackup = exportBackup;
        _importBackup = importBackup;

        Text = "Setup";
        Width = 820;
        Height = 620;
        MinimumSize = new Size(720, 520);
        AppTheme.ApplyWindow(this);

        BuildLayout();
        RefreshCredentials();
        RefreshGroups();
        RefreshImportDefaults();
    }

    private void BuildLayout()
    {
        var root = AppTheme.Card();
        var title = new Label
        {
            Text = "Setup",
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var description = new Label
        {
            Text = "Zentrale Einstellungen, Import und Backup.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = AppTheme.UiFont,
        };
        tabs.TabPages.Add(CredentialsTab());
        tabs.TabPages.Add(GroupsTab());
        tabs.TabPages.Add(AdImportTab());
        tabs.TabPages.Add(BackupTab());

        var buttons = AppTheme.Footer();
        var close = AppTheme.Button("Fertig", primary: true);
        close.DialogResult = DialogResult.OK;
        buttons.Controls.Add(close);

        root.Controls.Add(tabs);
        root.Controls.Add(description);
        root.Controls.Add(title);
        Controls.Add(root);
        Controls.Add(buttons);
        AcceptButton = close;
    }

    private TabPage CredentialsTab()
    {
        var page = Page("Zugaenge");
        _credentials.Dock = DockStyle.Fill;
        _credentials.BorderStyle = BorderStyle.None;
        _credentials.BackColor = AppTheme.SurfaceAlt;
        _credentials.DrawMode = DrawMode.OwnerDrawFixed;
        _credentials.ItemHeight = 58;
        _credentials.IntegralHeight = false;
        _credentials.DrawItem += DrawCredentialItem;
        _credentials.DoubleClick += (_, _) => EditCredential();

        _globalCredential.DropDownStyle = ComboBoxStyle.DropDownList;
        _globalCredential.FlatStyle = FlatStyle.Flat;
        AppTheme.StyleInput(_globalCredential);
        _globalCredential.SelectedIndexChanged += GlobalCredentialChanged;

        var globalPanel = Field("Globaler Standard-Zugang", _globalCredential);
        var listWrap = ListWrap(_credentials);
        var actions = ActionRow(
            ("Neu", AddCredential, true),
            ("Bearbeiten", EditCredential, false),
            ("Entfernen", RemoveCredential, false));

        page.Controls.Add(listWrap);
        page.Controls.Add(actions);
        page.Controls.Add(globalPanel);
        return page;
    }

    private TabPage GroupsTab()
    {
        var page = Page("Gruppen");
        _groups.Dock = DockStyle.Fill;
        _groups.BorderStyle = BorderStyle.None;
        _groups.BackColor = AppTheme.SurfaceAlt;
        _groups.DrawMode = DrawMode.OwnerDrawFixed;
        _groups.ItemHeight = 58;
        _groups.IntegralHeight = false;
        _groups.DrawItem += DrawGroupItem;
        _groups.DoubleClick += (_, _) => EditGroup();

        var actions = ActionRow(
            ("Neu", AddGroup, true),
            ("Bearbeiten", EditGroup, false),
            ("Entfernen", RemoveGroup, false));
        page.Controls.Add(ListWrap(_groups));
        page.Controls.Add(actions);
        return page;
    }

    private TabPage AdImportTab()
    {
        var page = Page("AD Import");
        _ldapPath.PlaceholderText = "LDAP://OU=Servers,DC=example,DC=local";
        _ldapFilter.Text = "(&(objectCategory=computer)(dNSHostName=*))";
        _adPreview.Dock = DockStyle.Fill;
        _adPreview.BorderStyle = BorderStyle.None;
        _adPreview.BackColor = AppTheme.SurfaceAlt;
        _adPreview.Font = AppTheme.UiFont;
        _adPreview.ItemHeight = 24;

        _importGroup.DropDownStyle = ComboBoxStyle.DropDownList;
        _importGroup.FlatStyle = FlatStyle.Flat;
        AppTheme.StyleInput(_importGroup);

        _importCredential.DropDownStyle = ComboBoxStyle.DropDownList;
        _importCredential.FlatStyle = FlatStyle.Flat;
        AppTheme.StyleInput(_importCredential);

        var actions = ActionRow(
            ("AD suchen", SearchAd, true),
            ("Vorschau importieren", ImportPreview, false),
            ("CSV importieren", LoadCsv, false));
        page.Controls.Add(ListWrap(_adPreview));
        page.Controls.Add(actions);
        page.Controls.Add(Field("Import-Zugang", _importCredential));
        page.Controls.Add(Field("Import-Gruppe", _importGroup));
        page.Controls.Add(Field("LDAP-Filter", _ldapFilter));
        page.Controls.Add(Field("OU / LDAP", _ldapPath));
        return page;
    }

    private TabPage BackupTab()
    {
        var page = Page("Backup");
        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Text = "Backup sichert Maschinen, Gruppen, Zugangsdaten und Wiederverbindungsstatus.",
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };
        var actions = ActionRow(
            ("Backup erstellen", _exportBackup, true),
            ("Backup wiederherstellen", _importBackup, false));
        page.Controls.Add(actions);
        page.Controls.Add(info);
        return page;
    }

    private void RefreshCredentials()
    {
        _credentials.DataSource = null;
        _credentials.DataSource = _data.Credentials.OrderBy(credential => credential.DisplayName).ToList();
        RefreshGlobalCredential();
        RefreshImportCredentials();
        _groups.Invalidate();
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

    private void RefreshGroups()
    {
        _groups.DataSource = null;
        _groups.DataSource = _data.Groups.OrderBy(group => group.DisplayName).ToList();
        RefreshImportGroups();
    }

    private void RefreshImportDefaults()
    {
        RefreshImportGroups();
        RefreshImportCredentials();
    }

    private void RefreshImportGroups()
    {
        var selectedId = (_importGroup.SelectedItem as GroupChoice)?.Id;
        _importGroup.Items.Clear();
        _importGroup.Items.Add(new GroupChoice(null, "Keine Gruppe"));
        foreach (var group in _data.Groups.OrderBy(group => group.DisplayName))
        {
            _importGroup.Items.Add(new GroupChoice(group.Id, group.DisplayName));
        }
        SelectComboItem(_importGroup, selectedId);
    }

    private void RefreshImportCredentials()
    {
        var selectedId = (_importCredential.SelectedItem as CredentialChoice)?.Id;
        _importCredential.Items.Clear();
        _importCredential.Items.Add(new CredentialChoice(null, "Gruppe/global verwenden"));
        foreach (var credential in _data.Credentials.OrderBy(credential => credential.DisplayName))
        {
            _importCredential.Items.Add(new CredentialChoice(credential.Id, credential.DisplayName));
        }
        SelectComboItem(_importCredential, selectedId);
    }

    private static void SelectComboItem(ComboBox comboBox, Guid? selectedId)
    {
        for (var index = 0; index < comboBox.Items.Count; index++)
        {
            var id = comboBox.Items[index] switch
            {
                CredentialChoice credential => credential.Id,
                GroupChoice group => group.Id,
                _ => null,
            };
            if (id == selectedId)
            {
                comboBox.SelectedIndex = index;
                return;
            }
        }

        if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private void GlobalCredentialChanged(object? sender, EventArgs e)
    {
        if (_globalCredential.SelectedItem is CredentialChoice choice)
        {
            _data.GlobalCredentialProfileId = choice.Id;
            DataChanged = true;
        }
    }

    private CredentialProfile? SelectedCredential() => _credentials.SelectedItem as CredentialProfile;

    private MachineGroup? SelectedGroup() => _groups.SelectedItem as MachineGroup;

    private void AddCredential()
    {
        using var dialog = new CredentialEditForm(null);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Credential is null)
        {
            return;
        }
        _data.Credentials.Add(dialog.Credential);
        DataChanged = true;
        RefreshCredentials();
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
        DataChanged = true;
        RefreshCredentials();
    }

    private void RemoveCredential()
    {
        var credential = SelectedCredential();
        if (credential is null)
        {
            return;
        }

        if (MessageBox.Show(this, "Zugang wirklich entfernen?", "RDP Man", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
        DataChanged = true;
        RefreshCredentials();
    }

    private void AddGroup()
    {
        using var dialog = new GroupEditForm(_data, null);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Group is null)
        {
            return;
        }
        _data.Groups.Add(dialog.Group);
        DataChanged = true;
        RefreshGroups();
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
        foreach (var machine in _data.Machines.Where(machine => machine.GroupId == group.Id))
        {
            machine.GroupName = group.Name;
        }
        DataChanged = true;
        RefreshGroups();
    }

    private void RemoveGroup()
    {
        var group = SelectedGroup();
        if (group is null)
        {
            return;
        }

        if (MessageBox.Show(this, $"Gruppe \"{group.DisplayName}\" entfernen?", "RDP Man", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        foreach (var machine in _data.Machines.Where(machine => machine.GroupId == group.Id))
        {
            machine.GroupId = null;
            machine.GroupName = "";
        }
        _data.Groups.RemoveAll(item => item.Id == group.Id);
        DataChanged = true;
        RefreshGroups();
    }

    private void SearchAd()
    {
        if (string.IsNullOrWhiteSpace(_ldapPath.Text))
        {
            MessageBox.Show(this, "Bitte LDAP-Pfad der OU angeben.", "RDP Man", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _previewNames.Clear();
            _adPreview.Items.Clear();
            using var root = new DirectoryEntry(_ldapPath.Text.Trim());
            using var searcher = new DirectorySearcher(root)
            {
                Filter = string.IsNullOrWhiteSpace(_ldapFilter.Text) ? "(&(objectCategory=computer)(dNSHostName=*))" : _ldapFilter.Text.Trim(),
                PageSize = 500,
            };
            searcher.PropertiesToLoad.Add("dNSHostName");
            searcher.PropertiesToLoad.Add("name");

            foreach (SearchResult result in searcher.FindAll())
            {
                var name = Value(result, "dNSHostName") ?? Value(result, "name");
                AddPreviewName(name);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AD Import fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadCsv()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "AD CSV Export importieren",
            Filter = "CSV-Dateien (*.csv;*.txt)|*.csv;*.txt|Alle Dateien (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _previewNames.Clear();
            _adPreview.Items.Clear();
            foreach (var name in ReadComputerNamesFromCsv(dialog.FileName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name))
            {
                AddPreviewName(name);
            }
            ImportPreviewNames("CSV Import");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "CSV Import fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportPreview()
    {
        ImportPreviewNames("AD Import");
    }

    private void ImportPreviewNames(string title)
    {
        var existingDns = _data.Machines.Select(machine => machine.DnsName.ToLowerInvariant()).ToHashSet();
        var groupId = (_importGroup.SelectedItem as GroupChoice)?.Id;
        var groupName = groupId is null
            ? ""
            : _data.Groups.FirstOrDefault(group => group.Id == groupId)?.Name ?? "";
        var credentialId = (_importCredential.SelectedItem as CredentialChoice)?.Id;
        var imported = 0;
        foreach (var name in _previewNames)
        {
            if (existingDns.Contains(name.ToLowerInvariant()))
            {
                continue;
            }

            _data.Machines.Add(new MachineEntry
            {
                Name = name,
                DnsName = name,
                GroupId = groupId,
                GroupName = groupName,
                CredentialProfileId = credentialId,
            });
            existingDns.Add(name.ToLowerInvariant());
            imported++;
        }

        if (imported > 0)
        {
            DataChanged = true;
        }
        MessageBox.Show(this, $"{imported} Rechner uebernommen.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void AddPreviewName(string? value)
    {
        var name = CleanComputerName(value);
        if (string.IsNullOrWhiteSpace(name) || _previewNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _previewNames.Add(name);
        _adPreview.Items.Add(name);
    }

    private void DrawCredentialItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _credentials.Items.Count)
        {
            return;
        }

        var credential = (CredentialProfile)_credentials.Items[e.Index];
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

    private void DrawGroupItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _groups.Items.Count)
        {
            return;
        }

        var group = (MachineGroup)_groups.Items[e.Index];
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

    private static TabPage Page(string title)
    {
        return new TabPage(title)
        {
            BackColor = AppTheme.Surface,
            Padding = new Padding(16),
        };
    }

    private static Panel ListWrap(Control list)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.SurfaceAlt,
            Padding = new Padding(8),
        };
        panel.Controls.Add(list);
        return panel;
    }

    private static FlowLayoutPanel ActionRow(params (string Text, Action Action, bool Primary)[] actions)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 50,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.Surface,
            Padding = new Padding(0, 8, 0, 4),
        };

        foreach (var action in actions)
        {
            var button = AppTheme.Button(action.Text, action.Primary);
            button.Width = Math.Max(116, action.Text.Length * 9 + 34);
            button.Click += (_, _) => action.Action();
            row.Controls.Add(button);
        }

        return row;
    }

    private static Panel Field(string label, Control input)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(0, 4, 0, 0),
        };
        var labelControl = AppTheme.Label(label);
        labelControl.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }

    private sealed record CredentialChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record GroupChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }

    private static string? Value(SearchResult result, string property)
    {
        return result.Properties.Contains(property) && result.Properties[property].Count > 0
            ? result.Properties[property][0]?.ToString()
            : null;
    }

    private static IEnumerable<string> ReadComputerNamesFromCsv(string fileName)
    {
        using var parser = new TextFieldParser(fileName)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true,
        };
        parser.SetDelimiters(",", ";", "\t");

        var firstRow = parser.ReadFields();
        if (firstRow is null)
        {
            yield break;
        }

        var column = FindComputerColumn(firstRow);
        if (column < 0)
        {
            var firstValue = CleanComputerName(firstRow.FirstOrDefault(field => !string.IsNullOrWhiteSpace(field)));
            if (!string.IsNullOrWhiteSpace(firstValue))
            {
                yield return firstValue;
            }
        }

        while (!parser.EndOfData)
        {
            var row = parser.ReadFields();
            if (row is null || row.Length == 0)
            {
                continue;
            }

            var rawValue = column >= 0 && column < row.Length
                ? row[column]
                : row.FirstOrDefault(field => !string.IsNullOrWhiteSpace(field));
            var computerName = CleanComputerName(rawValue);
            if (!string.IsNullOrWhiteSpace(computerName))
            {
                yield return computerName;
            }
        }
    }

    private static int FindComputerColumn(string[] fields)
    {
        string[] names = ["dnshostname", "hostname", "computername", "computer", "samaccountname", "name", "cn"];
        var normalized = fields.Select(field => new string((field ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray())).ToArray();
        foreach (var name in names)
        {
            var index = Array.IndexOf(normalized, name);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static string? CleanComputerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var clean = value.Trim().Trim('"');
        return clean.EndsWith("$", StringComparison.Ordinal) ? clean[..^1] : clean;
    }
}
