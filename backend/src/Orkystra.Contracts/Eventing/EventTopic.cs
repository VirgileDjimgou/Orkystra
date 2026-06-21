using System.Text;

namespace Orkystra.Contracts.Eventing;

public static class EventTopic
{
    public const string Root = "orkystra";
    public const string EventsStream = "events";
    public const string CommandsStream = "commands";
    public const string SnapshotsStream = "snapshots";

    public static string BuildEventTopic(
        string boundedContext,
        string aggregateType,
        string eventType,
        int schemaVersion)
    {
        return string.Join(
            '/',
            Root,
            EventsStream,
            ToKebabCase(boundedContext),
            ToKebabCase(aggregateType),
            ToKebabCase(eventType),
            $"v{schemaVersion}");
    }

    public static string ToKebabCase(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = new StringBuilder(value.Length + 8);

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            if (char.IsWhiteSpace(current) || current == '_' || current == '-' || current == '.')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                continue;
            }

            if (char.IsUpper(current))
            {
                var hasPrevious = builder.Length > 0;
                var previous = index > 0 ? value[index - 1] : '\0';
                var next = index < value.Length - 1 ? value[index + 1] : '\0';
                var shouldInsertDash =
                    hasPrevious &&
                    builder[^1] != '-' &&
                    (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && char.IsLower(next)));

                if (shouldInsertDash)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString().Trim('-');
    }
}
