using System.Globalization;
using System.Text;

namespace FleetOps.Api.Fleet;

/// <summary>
/// Simple RFC-4180-ish CSV parser tolerant to quoted fields, blank lines, and headers.
/// Used for idempotent fleet imports keyed on a natural business key.
/// </summary>
internal static class CsvFleetImporter
{
    public static IReadOnlyList<string[]> Parse(string csv, int minimumColumns)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        var rows = new List<string[]>();
        using var reader = new StringReader(NormalizeLineEndings(csv));
        string? line;
        var first = true;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseLine(line);
            if (first)
            {
                first = false;
                if (LooksLikeHeader(fields))
                {
                    continue;
                }
            }

            if (fields.Count < minimumColumns)
            {
                throw new InvalidDataException(
                    $"Row expects at least {minimumColumns} columns but got {fields.Count}: '{line}'.");
            }

            rows.Add([.. fields]);
        }

        return rows;
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = false;
                }
                else
                {
                    builder.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(builder.ToString().Trim());
                builder.Clear();
            }
            else
            {
                builder.Append(c);
            }
        }

        fields.Add(builder.ToString().Trim());
        return fields;
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> fields)
    {
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            var first = field.Trim()[0];
            if (char.IsLetter(first) && !IsLicenseLike(field) && !IsRegistrationLike(field))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLicenseLike(string field) =>
        field.Contains('-') && !field.Contains(' ');

    private static bool IsRegistrationLike(string field) =>
        !field.Contains(' ') && field.Contains('-');

    private static string NormalizeLineEndings(string csv) =>
        csv.Replace("\r\n", "\n").Replace('\r', '\n');

    public static string FormatImportSummary(ImportSummary summary, string targetType) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Imported {0}: {1} created, {2} updated, {3} skipped, {4} errors.",
            targetType,
            summary.Created,
            summary.Updated,
            summary.Skipped,
            summary.Errors.Count);
}
