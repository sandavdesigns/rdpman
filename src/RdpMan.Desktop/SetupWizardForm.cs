namespace RdpMan.Desktop;

public sealed class SetupWizardForm : Form
{
    private readonly AppData _data;
    private readonly TextBox _label = AppTheme.TextBox();
    private readonly TextBox _domain = AppTheme.TextBox();
    private readonly TextBox _username = AppTheme.TextBox();
    private readonly TextBox _password = AppTheme.TextBox();
    private readonly ListBox _preview = new();
    private readonly Label _csvStatus = new();
    private readonly List<string> _csvNames = [];
    private readonly CheckBox _setGlobalCredential = new();

    public bool DataChanged { get; private set; }

    public SetupWizardForm(AppData data)
    {
        _data = data;

        Text = $"{Brand.AppName} einrichten";
        Width = 760;
        Height = 620;
        MinimumSize = new Size(680, 540);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AppTheme.ApplyWindow(this);

        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = AppTheme.Card();
        root.Dock = DockStyle.Fill;

        var title = new Label
        {
            Text = "Ersteinrichtung",
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var subtitle = new Label
        {
            Text = "Standard-Zugang optional anlegen und Rechner direkt aus einer AD-CSV uebernehmen.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = AppTheme.UiFont,
        };
        tabs.TabPages.Add(CredentialPage());
        tabs.TabPages.Add(CsvPage());

        var buttons = AppTheme.Footer();
        var finish = AppTheme.Button("Fertig", primary: true);
        var cancel = AppTheme.Button("Abbrechen");
        finish.Click += (_, _) => FinishWizard();
        cancel.Click += (_, _) => CancelWizard();
        buttons.Controls.Add(finish);
        buttons.Controls.Add(cancel);

        root.Controls.Add(tabs);
        root.Controls.Add(subtitle);
        root.Controls.Add(title);
        Controls.Add(root);
        Controls.Add(buttons);
    }

    private TabPage CredentialPage()
    {
        var page = Page("Zugang");
        var intro = Info("Wenn du hier einen Zugang eintraegst, wird er als globaler Standard fuer neue Verbindungen verwendet. Du kannst den Schritt leer lassen.");

        _label.PlaceholderText = "z.B. Standard";
        _domain.PlaceholderText = "optional";
        _username.PlaceholderText = "Benutzername";
        _password.UseSystemPasswordChar = true;
        _password.PlaceholderText = "optional";

        StyleCheckBox(_setGlobalCredential, "Als globalen Standard-Zugang verwenden");
        _setGlobalCredential.Checked = true;

        page.Controls.Add(_setGlobalCredential);
        page.Controls.Add(Field("Passwort", _password));
        page.Controls.Add(Field("Benutzer", _username));
        page.Controls.Add(Field("Domain", _domain));
        page.Controls.Add(Field("Label", _label));
        page.Controls.Add(intro);
        return page;
    }

    private TabPage CsvPage()
    {
        var page = Page("CSV Import");
        var intro = Info("CSV oder TXT aus dem AD-Export waehlen. Bekannte Spalten wie DNSHostName, ComputerName, Name, CN oder sAMAccountName werden erkannt.");

        _preview.Dock = DockStyle.Fill;
        _preview.BorderStyle = BorderStyle.None;
        _preview.BackColor = AppTheme.SurfaceAlt;
        _preview.Font = AppTheme.UiFont;

        _csvStatus.Dock = DockStyle.Bottom;
        _csvStatus.Height = 28;
        _csvStatus.ForeColor = AppTheme.MutedText;
        _csvStatus.Font = AppTheme.SmallFont;
        _csvStatus.Text = "Noch keine CSV ausgewaehlt.";

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.Surface,
            Padding = new Padding(0, 10, 0, 6),
        };
        var choose = AppTheme.Button("CSV waehlen", primary: true);
        var clear = AppTheme.Button("Auswahl leeren");
        choose.Width = 128;
        clear.Width = 128;
        choose.Click += (_, _) => ChooseCsv();
        clear.Click += (_, _) => ClearCsv();
        actions.Controls.Add(choose);
        actions.Controls.Add(clear);

        var listWrap = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.SurfaceAlt,
            Padding = new Padding(8),
        };
        listWrap.Controls.Add(_preview);

        page.Controls.Add(listWrap);
        page.Controls.Add(_csvStatus);
        page.Controls.Add(actions);
        page.Controls.Add(intro);
        return page;
    }

    private void ChooseCsv()
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
            _csvNames.Clear();
            _csvNames.AddRange(CsvComputerImport.ReadComputerNames(dialog.FileName));
            RefreshCsvPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "CSV Import fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearCsv()
    {
        _csvNames.Clear();
        RefreshCsvPreview();
    }

    private void RefreshCsvPreview()
    {
        _preview.Items.Clear();
        foreach (var name in _csvNames)
        {
            _preview.Items.Add(name);
        }

        _csvStatus.Text = _csvNames.Count == 0
            ? "Noch keine Rechner ausgewaehlt."
            : $"{_csvNames.Count} Rechner bereit zum Import.";
    }

    private void FinishWizard()
    {
        if (!AddCredentialIfNeeded())
        {
            return;
        }

        ImportCsvMachines();
        _data.SetupWizardCompleted = true;
        DataChanged = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelWizard()
    {
        _data.SetupWizardCompleted = true;
        DataChanged = true;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private bool AddCredentialIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(_username.Text) && string.IsNullOrWhiteSpace(_password.Text))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_username.Text))
        {
            MessageBox.Show(this, "Benutzername ist erforderlich, wenn Zugangsdaten gespeichert werden sollen.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var credential = new CredentialProfile
        {
            Label = string.IsNullOrWhiteSpace(_label.Text) ? "Standard" : _label.Text.Trim(),
            Domain = _domain.Text.Trim(),
            Username = _username.Text.Trim(),
            ProtectedPassword = string.IsNullOrWhiteSpace(_password.Text) ? "" : CredentialVault.Protect(_password.Text),
        };
        _data.Credentials.Add(credential);
        if (_setGlobalCredential.Checked)
        {
            _data.GlobalCredentialProfileId = credential.Id;
        }

        return true;
    }

    private void ImportCsvMachines()
    {
        if (_csvNames.Count == 0)
        {
            return;
        }

        var existingDns = _data.Machines.Select(machine => machine.DnsName.ToLowerInvariant()).ToHashSet();
        foreach (var name in _csvNames)
        {
            if (existingDns.Contains(name.ToLowerInvariant()))
            {
                continue;
            }

            _data.Machines.Add(new MachineEntry
            {
                Name = name,
                DnsName = name,
                UseGlobalRedirectSettings = true,
            });
            existingDns.Add(name.ToLowerInvariant());
        }
    }

    private static TabPage Page(string title)
    {
        return new TabPage(title)
        {
            BackColor = AppTheme.Surface,
            Padding = new Padding(16),
        };
    }

    private static Label Info(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 48,
            Font = AppTheme.UiFont,
            ForeColor = AppTheme.MutedText,
        };
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

    private static void StyleCheckBox(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.AutoSize = true;
        checkBox.Dock = DockStyle.Top;
        checkBox.Font = AppTheme.UiFont;
        checkBox.ForeColor = AppTheme.Text;
        checkBox.Margin = new Padding(0, 4, 0, 8);
    }
}
