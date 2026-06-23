using Microsoft.VisualBasic.FileIO;

namespace RdpMan.Desktop;

internal static class CsvComputerImport
{
    public static List<string> ReadComputerNames(string fileName)
    {
        return ReadComputerNamesFromCsv(fileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();
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
