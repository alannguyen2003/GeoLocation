using LocationTracker.Business.Models;

namespace LocationTracker.Business.Validation;

public static class LocationRules
{
    public const int MinBatchSize = 1;
    public const int MaxBatchSize = 100;
    public const int DefaultQueryLimit = 100;
    public const int MaxQueryLimit = 500;

    /// <summary>Beyond this the nearest place says little (open sea), so no city or country is stored.</summary>
    public const double MaxPlaceDistanceKm = 100;
    public const int MaxDistrictLength = 100;

    public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    /// <summary>
    /// The district comes from the phone's geocoder and is only a nice-to-have, so it is trimmed
    /// and shortened rather than rejected: a bad district must never cost the user a point.
    /// </summary>
    public static string? NormalizeDistrict(string? district)
    {
        var trimmed = district?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaxDistrictLength ? trimmed : trimmed[..MaxDistrictLength];
    }

    public static void ValidateBatch(IReadOnlyList<NewLocationPoint>? points, DateTimeOffset now)
    {
        if (points is null || points.Count is < MinBatchSize or > MaxBatchSize)
        {
            throw new BusinessValidationException(
                "points",
                $"A batch must contain between {MinBatchSize} and {MaxBatchSize} points.");
        }

        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < points.Count; i++)
        {
            AddPointErrors(errors, points[i], now, $"points[{i}]");
        }

        if (errors.Count > 0)
        {
            throw new BusinessValidationException(errors);
        }
    }

    /// <summary>Validates the query and returns the limit to use.</summary>
    public static int ValidateQuery(DateTimeOffset? from, DateTimeOffset? to, int? limit)
    {
        var errors = new Dictionary<string, string[]>();

        if (limit is < 1 or > MaxQueryLimit)
        {
            errors["limit"] = [$"limit must be between 1 and {MaxQueryLimit}."];
        }

        if (from is { } f && to is { } t && f > t)
        {
            errors["from"] = ["from must not be later than to."];
        }

        if (errors.Count > 0)
        {
            throw new BusinessValidationException(errors);
        }

        return limit ?? DefaultQueryLimit;
    }

    private static void AddPointErrors(
        Dictionary<string, string[]> errors,
        NewLocationPoint point,
        DateTimeOffset now,
        string prefix)
    {
        if (point.ClientPointId == Guid.Empty)
        {
            errors[$"{prefix}.clientPointId"] = ["clientPointId is required."];
        }

        // Written as "not inside the range" so NaN is rejected too.
        if (!(point.Latitude >= -90 && point.Latitude <= 90))
        {
            errors[$"{prefix}.latitude"] = ["latitude must be between -90 and 90."];
        }

        if (!(point.Longitude >= -180 && point.Longitude <= 180))
        {
            errors[$"{prefix}.longitude"] = ["longitude must be between -180 and 180."];
        }

        if (point.Accuracy is { } accuracy && !(accuracy >= 0))
        {
            errors[$"{prefix}.accuracy"] = ["accuracy must be 0 or greater."];
        }

        if (point.RecordedAt > now + MaxFutureSkew)
        {
            errors[$"{prefix}.recordedAt"] = ["recordedAt must not be more than 5 minutes in the future."];
        }
        else if (point.RecordedAt < now - MaxAge)
        {
            errors[$"{prefix}.recordedAt"] = ["recordedAt must not be older than 7 days."];
        }

        if (point.Source is null || !LocationSource.All.Contains(point.Source))
        {
            errors[$"{prefix}.source"] = ["source must be one of: foreground, background, web."];
        }
    }
}
