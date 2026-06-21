using Orkystra.Domain.Common;

namespace Orkystra.Domain.ValueObjects;

public readonly record struct TimeWindow
{
    private TimeWindow(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public DateTimeOffset StartsAt { get; }

    public DateTimeOffset EndsAt { get; }

    public TimeSpan Duration => EndsAt - StartsAt;

    public static Result<TimeWindow> Create(DateTimeOffset startsAt, DateTimeOffset endsAt) =>
        endsAt < startsAt
            ? Result.Failure<TimeWindow>(DomainErrors.InvalidValue(nameof(TimeWindow), "end must be after or equal to start"))
            : Result.Success(new TimeWindow(startsAt, endsAt));
}
