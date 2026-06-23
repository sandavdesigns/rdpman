using System.DirectoryServices;
using Microsoft.VisualBasic.FileIO;

namespace RdpMan.Desktop;

public sealed class AdImportForm : Form
{
    private readonly TextBox _ldapPath = AppTheme.TextBox();
    private readonly TextBox _filter = AppTheme.TextBox();
    private readonly ListBox _preview = new();

    public List<string> ImportedDnsNames { get; } = [];

    public AdImportForm()
    {
        Text = "AD Import";
        Width = 760;
        Height = 560;
        MinimumSize = new Size(680, 500);
        AppTheme.ApplyWindow(this);

        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = AppTheme.Card();
        var title = new Label
        {
            Text = "Active Directory Import",
            Dock = DockStyle.Top,
            Height = 34,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
        };
        var description = new Label
        {
            Text = "Computerobjekte aus einer OU suchen und als Maschinen übernehmen.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = AppTheme.MutedText,
            Font = AppTheme.UiFont,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Height = 124,
            Padding = new Padding(0, 12, 0, 0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _ldapPath.PlaceholderText = "LDAP://OU=Servers,DC=example,DC=local";
        _filter.Text = "(&(objectCategory=computer)(dNSHostName=*))";
        _preview.Dock = DockStyle.Fill;
        _preview.BorderStyle = BorderStyle.None;
        _preview.BackColor = AppTheme.SurfaceAlt;
        _preview.Font = AppTheme.UiFont;
        _preview.ItemHeight = 28;

        layout.Controls.Add(Field("OU / LDAP", _ldapPath), 0, 0);
        layout.Controls.Add(Field("LDAP-Filter", _filter), 0, 1);

        var previewWrap = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.SurfaceAlt,
            Padding = new Padding(10),
        };
        previewWrap.Controls.Add(_preview);

        var previewTitle = new Label
        {
            Text = "Vorschau",
            Dock = DockStyle.Top,
            Height = 28,
            Font = AppTheme.SectionFont,
            ForeColor = AppTheme.Text,
        };

        var buttons = AppTheme.Footer();
        var import = AppTheme.Button("Importieren", primary: true);
        var search = AppTheme.Button("Suchen");
        var csv = AppTheme.Button("CSV importieren");
        var cancel = AppTheme.Button("Abbrechen");
        import.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        search.Click += (_, _) => Search();
        csv.Click += (_, _) => ImportCsv();
        import.Click += (_, _) => Import();
        buttons.Controls.Add(import);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(csv);
        buttons.Controls.Add(search);

        AcceptButton = import;
        CancelButton = cancel;
        root.Controls.Add(previewWrap);
        root.Controls.Add(previewTitle);
        root.Controls.Add(layout);
        root.Controls.Add(description);
        root.Controls.Add(title);
        Controls.Add(root);
        Controls.Add(buttons);
    }

    private void Search()
    {
        _preview.Items.Clear();
        ImportedDnsNames.Clear();

        if (string.IsNullOrWhiteSpace(_ldapPath.Text))
        {
            MessageBox.Show(this, "Bitte LDAP-Pfad der OU angeben.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private void ImportCsv()
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
            var names = ReadComputerNamesFromCsv(dialog.FileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            ImportedDnsNames.Clear();
            _preview.Items.Clear();
            foreach (var name in names)
            {
                ImportedDnsNames.Add(name);
                _preview.Items.Add(name);
            }

            if (ImportedDnsNames.Count == 0)
            {
                MessageBox.Show(this, "In der CSV wurden keine Computer gefunden.", Brand.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "CSV Import fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

        string[]? header = null;
        int nameColumn = 0;
        var firstRow = parser.ReadFields();
        if (firstRow is null)
        {
            yield break;
        }

        var headerColumn = FindComputerColumn(firstRow);
        if (headerColumn >= 0)
        {
            header = firstRow;
            nameColumn = headerColumn;
        }
        else
        {
            var firstValue = CleanComputerName(FirstUsefulField(firstRow));
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

            var rawValue = header is null
                ? FirstUsefulField(row)
                : (nameColumn < row.Length ? row[nameColumn] : null);
            var computerName = CleanComputerName(rawValue);
            if (!string.IsNullOrWhiteSpace(computerName))
            {
                yield return computerName;
            }
        }
    }

    private static int FindComputerColumn(string[] fields)
    {
        string[] preferredColumns =
        [
            "dnshostname",
            "dnshost",
            "hostname",
            "computername",
            "computer",
            "samaccountname",
            "name",
            "cn",
        ];

        var normalized = fields.Select(NormalizeColumnName).ToArray();
        foreach (var preferred in preferredColumns)
        {
            var index = Array.IndexOf(normalized, preferred);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static string? FirstUsefulField(string[] fields)
    {
        return fields.FirstOrDefault(field => !string.IsNullOrWhiteSpace(field));
    }

    private static string? CleanComputerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var clean = value.Trim().Trim('"');
        if (clean.EndsWith("$", StringComparison.Ordinal))
        {
            clean = clean[..^1];
        }

        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }

    private static string NormalizeColumnName(string? value)
    {
        return new string((value ?? "")
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string? Value(SearchResult result, string property)
    {
        return result.Properties.Contains(property) && result.Properties[property].Count > 0
            ? result.Properties[property][0]?.ToString()
            : null;
    }

    private static Panel Field(string label, TextBox input)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 58 };
        var labelControl = AppTheme.Label(label);
        labelControl.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;
        panel.Controls.Add(input);
        panel.Controls.Add(labelControl);
        return panel;
    }
}
