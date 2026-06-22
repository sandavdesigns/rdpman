namespace RdpMan.Desktop;

public sealed class SetupForm : Form
{
    private readonly AppData _data;
    private readonly Action _importFromAd;
    private readonly Action _exportBackup;
    private readonly Action _importBackup;

    public SetupForm(AppData data, Action importFromAd, Action exportBackup, Action importBackup)
    {
        _data = data;
        _importFromAd = importFromAd;
        _exportBackup = exportBackup;
        _importBackup = importBackup;

        Text = "Setup";
        Width = 720;
        Height = 480;
        MinimumSize = new Size(640, 420);
        AppTheme.ApplyWindow(this);

        BuildLayout();
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
        tabs.TabPages.Add(Tab("Zugaenge", "Zugaenge verwalten", "Globalen Standard-Zugang und gespeicherte Profile bearbeiten.", "Zugaenge oeffnen", () =>
        {
            using var dialog = new CredentialManagerForm(_data);
            dialog.ShowDialog(this);
        }));
        tabs.TabPages.Add(Tab("Gruppen", "Gruppen verwalten", "Gruppen zentral anlegen, Farben setzen und Zugangsdaten vererben.", "Gruppen oeffnen", () =>
        {
            using var dialog = new GroupManagerForm(_data);
            dialog.ShowDialog(this);
        }));
        tabs.TabPages.Add(Tab("AD Import", "Active Directory Import", "Computer aus einer OU oder aus einem CSV-Export in die Liste uebernehmen.", "AD Import starten", _importFromAd));
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

    private static TabPage Tab(string title, string heading, string text, string buttonText, Action action)
    {
        var page = new TabPage(title)
        {
            BackColor = AppTheme.Surface,
            Padding = new Padding(18),
        };

        var headingLabel = Heading(heading);
        var textLabel = Description(text);
        var button = AppTheme.Button(buttonText, primary: true);
        button.Width = 180;
        button.Click += (_, _) => action();

        page.Controls.Add(button);
        page.Controls.Add(textLabel);
        page.Controls.Add(headingLabel);
        button.Dock = DockStyle.Top;
        textLabel.Dock = DockStyle.Top;
        headingLabel.Dock = DockStyle.Top;
        return page;
    }

    private TabPage BackupTab()
    {
        var page = new TabPage("Backup")
        {
            BackColor = AppTheme.Surface,
            Padding = new Padding(18),
        };
        var headingLabel = Heading("Backup und Wiederherstellung");
        var textLabel = Description("Maschinen, Gruppen, Zugangsdaten und Wiederverbindungsstatus sichern oder wiederherstellen.");
        var restore = AppTheme.Button("Backup wiederherstellen");
        restore.Width = 190;
        restore.Click += (_, _) => _importBackup();
        var export = AppTheme.Button("Backup erstellen", primary: true);
        export.Width = 170;
        export.Click += (_, _) => _exportBackup();

        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.Surface,
        };
        row.Controls.Add(export);
        row.Controls.Add(restore);

        page.Controls.Add(row);
        page.Controls.Add(textLabel);
        page.Controls.Add(headingLabel);
        return page;
    }

    private static Label Heading(string text)
    {
        return new Label
        {
            Text = text,
            Height = 34,
            Font = AppTheme.SectionFont,
            ForeColor = AppTheme.Text,
        };
    }

    private static Label Description(string text)
    {
        return new Label
        {
            Text = text,
            Height = 46,
            Font = AppTheme.UiFont,
            ForeColor = AppTheme.MutedText,
        };
    }
}
