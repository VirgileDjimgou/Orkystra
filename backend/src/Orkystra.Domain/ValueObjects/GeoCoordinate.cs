using Orkystra.Domain.Common;

namespace Orkystra.Domain.ValueObjects;

public readonly record struct GeoCoordinate
{
    private GeoCoordinate(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public decimal Latitude { get; }

    public decimal Longitude { get; }

    public static Result<GeoCoordinate> Create(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90)
        {
            return Result.Failure<GeoCoordinate>(DomainErrors.InvalidValue(nameof(latitude), "latitude must be between -90 and 90"));
        }

        if (longitude is < -180 or > 180)
        {
            return Result.Failure<GeoCoordinate>(DomainErrors.InvalidValue(nameof(longitude), "longitude must be between -180 and 180"));
        }

        return Result.Success(new GeoCoordinate(latitude, longitude));
    }
}
