using System.Text;

namespace FleetOps.Api.Onboarding;

internal static class OnboardingCsvParser
{
    private static readonly Dictionary<string, string[]> Headers =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["vehicles"] = ["registrationNumber", "displayName"],
            ["drivers"] = ["fullName", "licenseNumber", "phoneNumber"],
            ["devices"] = ["serialNumber", "displayName"]
        };

    private static readonly Dictionary<string, int[]> MaximumLengths =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["vehicles"] = [32, 128],
            ["drivers"] = [160, 64, 40],
            ["devices"] = [64, 128]
        };

    public static string Template(string targetType)
    {
        var normalized = NormalizeTarget(targetType);
        return normalized switch
        {
            "vehicles" => "registrationNumber,displayName\nFLEET-001,Service Van 1\n",
            "drivers" => "fullName,licenseNumber,phoneNumber\nAlex Driver,LIC-001,\n",
            "devices" => "serialNumber,displayName\nGPS-001,Van tracker\n",
            _ => throw new ArgumentOutOfRangeException(nameof(targetType))
        };
    }

    public static ParsedOnboardingCsv Parse(string targetType, string csv)
    {
        var normalized = NormalizeTarget(targetType);
        if (Encoding.UTF8.GetByteCount(csv ?? string.Empty) > 1_048_576)
        {
            return new ParsedOnboardingCsv(normalized, [], [new ImportRowError(0, "file", "CSV must not exceed 1 MB.")]);
        }

        var lines = (csv ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var nonEmpty = lines.Select((value, index) => new { Value = value, Line = index + 1 })
            .Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList();
        if (nonEmpty.Count == 0)
        {
            return new ParsedOnboardingCsv(normalized, [], [new ImportRowError(0, "file", "CSV is empty.")]);
        }

        var expected = Headers[normalized];
        var header = ParseLine(nonEmpty[0].Value);
        if (header.Count != expected.Length || !header.Select(NormalizeHeader).SequenceEqual(expected.Select(NormalizeHeader)))
        {
            return new ParsedOnboardingCsv(
                normalized,
                [],
                [new ImportRowError(nonEmpty[0].Line, "header", $"Expected header: {string.Join(',', expected)}.")]);
        }

        var rows = new List<string[]>();
        var errors = new List<ImportRowError>();
        var naturalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in nonEmpty.Skip(1).Take(1000))
        {
            List<string> values;
            try
            {
                values = ParseLine(source.Value);
            }
            catch (InvalidDataException ex)
            {
                errors.Add(new ImportRowError(source.Line, "row", ex.Message));
                continue;
            }

            if (values.Count != expected.Length)
            {
                errors.Add(new ImportRowError(source.Line, "row", $"Expected {expected.Length} columns but found {values.Count}."));
                continue;
            }

            var row = values.Select(x => x.Trim()).ToArray();
            var requiredIndexes = normalized == "devices" ? new[] { 0 } : new[] { 0, 1 };
            foreach (var index in requiredIndexes.Where(index => string.IsNullOrWhiteSpace(row[index])))
            {
                errors.Add(new ImportRowError(source.Line, expected[index], "Value is required."));
            }

            var maximumLengths = MaximumLengths[normalized];
            for (var index = 0; index < row.Length; index++)
            {
                if (row[index].Length > maximumLengths[index])
                {
                    errors.Add(new ImportRowError(
                        source.Line,
                        expected[index],
                        $"Value must not exceed {maximumLengths[index]} characters."));
                }
            }

            var keyIndex = normalized == "drivers" ? 1 : 0;
            if (!string.IsNullOrWhiteSpace(row[keyIndex]) && !naturalKeys.Add(row[keyIndex]))
            {
                errors.Add(new ImportRowError(source.Line, expected[keyIndex], "Duplicate business key in this file."));
            }

            rows.Add(row);
        }

        if (nonEmpty.Count - 1 > 1000)
        {
            errors.Add(new ImportRowError(0, "file", "CSV must contain at most 1,000 data rows."));
        }

        return new ParsedOnboardingCsv(normalized, rows, errors);
    }

    public static string NormalizeTarget(string targetType) =>
        targetType?.Trim().ToLowerInvariant() switch
        {
            "vehicles" => "vehicles",
            "drivers" => "drivers",
            "devices" => "devices",
            _ => throw new ArgumentException("Target type must be vehicles, drivers, or devices.", nameof(targetType))
        };

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];
            if (current == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (current == ',' && !quoted)
            {
                fields.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(current);
            }
        }

        if (quoted)
        {
            throw new InvalidDataException("Quoted field is not closed.");
        }

        fields.Add(value.ToString());
        return fields;
    }

    private static string NormalizeHeader(string value) => value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}

internal sealed record ParsedOnboardingCsv(string TargetType, IReadOnlyList<string[]> Rows, IReadOnlyList<ImportRowError> Errors);
