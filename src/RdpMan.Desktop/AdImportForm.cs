using System.DirectoryServices;

namespace RdpMan.Desktop;

public sealed class AdImportForm : Form
{
    private readonly TextBox _ldapPath = new();
    private readonly TextBox _filter = new();
    private readonly ListBox _preview = new();

    public List<string> ImportedDnsNames { get; } = [];

    public AdImportForm()
    {
        Text = "AD Import";
        Width = 720;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;

        BuildLayout();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(14),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _ldapPath.PlaceholderText = "LDAP://OU=Servers,DC=example,DC=local";
        _filter.Text = "(&(objectCategory=computer)(dNSHostName=*))";
        _preview.Dock = DockStyle.Fill;

        layout.Controls.Add(new Label { Text = "OU / LDAP", AutoSize = true }, 0, 0);
        layout.Controls.Add(_ldapPath, 1, 0);
        layout.Controls.Add(new Label { Text = "Filter", AutoSize = true }, 0, 1);
        layout.Controls.Add(_filter, 1, 1);
        layout.Controls.Add(new Label { Text = "Vorschau", AutoSize = true }, 0, 2);
        layout.Controls.Add(_preview, 1, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var import = new Button { Text = "Importieren", DialogResult = DialogResult.OK };
        var search = new Button { Text = "Suchen" };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel };
        search.Click += (_, _) => Search();
        import.Click += (_, _) => Import();
        buttons.Controls.Add(import);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(search);
        layout.Controls.Add(buttons, 1, 3);

        AcceptButton = import;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    private void Search()
    {
        _preview.Items.Clear();
        ImportedDnsNames.Clear();

        if (string.IsNullOrWhiteSpace(_ldapPath.Text))
        {
            MessageBox.Show(this, "Bitte LDAP-Pfad der OU angeben.", "RDP Man", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var root = new DirectoryEntry(_ldapPath.Text.Trim());
            using var searcher = new DirectorySearcher(root)
            {
                Filter = string.IsNullOrWhiteSpace(_filter.Text) ? "(&(objectCategory=computer)(dNSHostName=*))" : _filter.Text.Trim(),
                PageSize = 500,
            };
            searcher.PropertiesToLoad.Add("dNSHostName");
            searcher.PropertiesToLoad.Add("name");

            foreach (SearchResult result in searcher.FindAll())
            {
                var dnsName = Value(result, "dNSHostName") ?? Value(result, "name");
                if (string.IsNullOrWhiteSpace(dnsName))
                {
                    continue;
                }
                ImportedDnsNames.Add(dnsName);
                _preview.Items.Add(dnsName);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AD Import fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Import()
    {
        if (ImportedDnsNames.Count == 0)
        {
            Search();
        }

        if (ImportedDnsNames.Count == 0)
        {
            DialogResult = DialogResult.None;
        }
    }

    private static string? Value(SearchResult result, string property)
    {
        return result.Properties.Contains(property) && result.Properties[property].Count > 0
            ? result.Properties[property][0]?.ToString()
            : null;
    }
}

