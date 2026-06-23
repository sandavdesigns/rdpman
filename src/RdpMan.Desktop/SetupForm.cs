using System.DirectoryServices;
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
    private readonly CheckBox _globalRedirectClipboard = new();
    private readonly CheckBox _globalRedirectPrinters = new();
    private readonly CheckBox _globalRedirectSmartCards = new();
    private readonly CheckBox _globalRedirectWebAuthn = new();
    private readonly CheckBox _rememberConnectedSessions = new();
    private readonly CheckBox _restoreConnectedSessionsOnStart = new();
    private readonly ListBox _autoReconnect = new();
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
        RefreshAutoReconnectList();
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
        tabs.TabPages.Add(RdpTab());
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

    private TabPage RdpTab()
    {
        var page = Page("RDP");
        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = "Diese Freigaben gelten fuer neue Rechner und fuer Eintraege, die globale Freigaben verwenden.",
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };

        StyleCheckBox(_globalRedirectClipboard, "Zwischenablage erlauben");
        StyleCheckBox(_globalRedirectPrinters, "Drucker umleiten");
        StyleCheckBox(_globalRedirectSmartCards, "Smartcards umleiten");
        StyleCheckBox(_globalRedirectWebAuthn, "WebAuthn / Windows Hello erlauben");
        StyleCheckBox(_rememberConnectedSessions, "Verbundene Rechner beim Beenden merken");
        StyleCheckBox(_restoreConnectedSessionsOnStart, "Gemerkte Rechner beim Start automatisch verbinden");

        _globalRedirectClipboard.Checked = _data.GlobalRedirectClipboard;
        _globalRedirectPrinters.Checked = _data.GlobalRedirectPrinters;
        _globalRedirectSmartCards.Checked = _data.GlobalRedirectSmartCards;
        _globalRedirectWebAuthn.Checked = _data.GlobalRedirectWebAuthn;
        _rememberConnectedSessions.Checked = _data.RememberConnectedSessions;
        _restoreConnectedSessionsOnStart.Checked = _data.RestoreConnectedSessionsOnStart;

        _globalRedirectClipboard.CheckedChanged += (_, _) => UpdateGlobalRedirects();
        _globalRedirectPrinters.CheckedChanged += (_, _) => UpdateGlobalRedirects();
        _globalRedirectSmartCards.CheckedChanged += (_, _) => UpdateGlobalRedirects();
        _globalRedirectWebAuthn.CheckedChanged += (_, _) => UpdateGlobalRedirects();
        _rememberConnectedSessions.CheckedChanged += (_, _) => UpdateSessionMemoryOptions();
        _restoreConnectedSessionsOnStart.CheckedChanged += (_, _) => UpdateSessionMemoryOptions();
        _autoReconnect.Dock = DockStyle.Fill;
        _autoReconnect.BorderStyle = BorderStyle.None;
        _autoReconnect.BackColor = AppTheme.SurfaceAlt;
        _autoReconnect.Font = AppTheme.UiFont;
        _autoReconnect.ItemHeight = 24;

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 116,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = AppTheme.Surface,
            Padding = new Padding(0, 8, 0, 0),
        };
        options.Controls.Add(_globalRedirectClipboard);
        options.Controls.Add(_globalRedirectPrinters);
        options.Controls.Add(_globalRedirectSmartCards);
        options.Controls.Add(_globalRedirectWebAuthn);

        var sessionOptions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 76,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = AppTheme.Surface,
            Padding = new Padding(0, 8, 0, 0),
        };
        sessionOptions.Controls.Add(_rememberConnectedSessions);
        sessionOptions.Controls.Add(_restoreConnectedSessionsOnStart);

        var autoReconnectActions = ActionRow(
            ("Entfernen", RemoveSelectedAutoReconnect, false),
            ("Fehlende entfernen", RemoveMissingAutoReconnect, false));
        var autoReconnectInfo = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "Gemerkte Wiederverbindungen. Fehlende Rechner koennen hier entfernt werden.",
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.SmallFont,
        };

        page.Controls.Add(ListWrap(_autoReconnect));
        page.Controls.Add(autoReconnectActions);
        page.Controls.Add(autoReconnectInfo);
        page.Controls.Add(sessionOptions);
        page.Controls.Add(options);
        page.Controls.Add(info);
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

    private void UpdateGlobalRedirects()
    {
        _data.GlobalRedirectClipboard = _globalRedirectClipboard.Checked;
        _data.GlobalRedirectPrinters = _globalRedirectPrinters.Checked;
        _data.GlobalRedirectSmartCards = _globalRedirectSmartCards.Checked;
        _data.GlobalRedirectWebAuthn = _globalRedirectWebAuthn.Checked;
        DataChanged = true;
    }

    private void UpdateSessionMemoryOptions()
    {
        _data.RememberConnectedSessions = _rememberConnectedSessions.Checked;
        _data.RestoreConnectedSessionsOnStart = _restoreConnectedSessionsOnStart.Checked;
        if (!_data.RememberConnectedSessions)
        {
            _data.AutoReconnectMachineIds.Clear();
            RefreshAutoReconnectList();
        }
        DataChanged = true;
    }

    private void RefreshAutoReconnectList()
    {
        var items = _data.AutoReconnectMachineIds
            .Distinct()
            .Select(id =>
            {
                var machine = _data.Machines.FirstOrDefault(item => item.Id == id);
                return new AutoReconnectChoice(
                    id,
                    machine?.DisplayName ?? $"Fehlender Rechner ({id})",
                    machine is null);
            })
            .OrderByDescending(item => item.Missing)
            .ThenBy(item => item.Label)
            .ToList();

        _autoReconnect.DataSource = null;
        _autoReconnect.DataSource = items;
    }

    private void RemoveSelectedAutoReconnect()
    {
        if (_autoReconnect.SelectedItem is not AutoReconnectChoice choice)
        {
            return;
        }

        if (_data.AutoReconnectMachineIds.RemoveAll(id => id == choice.Id) == 0)
        {
            return;
        }

        DataChanged = true;
        RefreshAutoReconnectList();
    }

    private void RemoveMissingAutoReconnect()
    {
        var existingIds = _data.Machines.Select(machine => machine.Id).ToHashSet();
        var removed = _data.AutoReconnectMachineIds.RemoveAll(id => !existingIds.Contains(id));
        if (removed == 0)
        {
            return;
        }

        DataChanged = true;
        RefreshAutoReconnectList();
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

        if (MessageBox.Show(this, "Zugang wirklich entfernen?", Brand.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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

        if (MessageBox.Show(this, $"Gruppe \"{group.DisplayName}\" entfernen?", Brand.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
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
            MessageBox.Show(this, "Bitte LDAP-Pfad der OU angeben.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            foreach (var name in CsvComputerImport.ReadComputerNames(dialog.FileName))
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
                UseGlobalRedirectSettings = true,
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

    private static void StyleCheckBox(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.AutoSize = true;
        checkBox.Font = AppTheme.UiFont;
        checkBox.ForeColor = AppTheme.Text;
        checkBox.Margin = new Padding(0, 4, 22, 8);
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

    private sealed record AutoReconnectChoice(Guid Id, string Label, bool Missing)
    {
        public override string ToString() => Missing ? $"! {Label}" : Label;
    }

    private static string? Value(SearchResult result, string property)
    {
        return result.Properties.Contains(property) && result.Properties[property].Count > 0
            ? result.Properties[property][0]?.ToString()
            : null;
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
