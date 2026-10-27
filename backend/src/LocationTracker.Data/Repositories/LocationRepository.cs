using System.Text;
using LocationTracker.Data.Entities;
using LocationTracker.Data.Places;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LocationTracker.Data.Repositories;

public class LocationRepository(AppDbContext db) : ILocationRepository
{
    private const int ColumnsPerRow = 16;

    public async Task<int> InsertIgnoringDuplicatesAsync(
        IReadOnlyList<LocationPoint> points,
        CancellationToken cancellationToken = default)
    {
        if (points.Count == 0)
        {
            return 0;
        }

        // A single multi-row INSERT with ON CONFLICT DO NOTHING keeps dedupe correct even when
        // the same batch is retried concurrently, which a "select existing, then insert" cannot.
        var sql = new StringBuilder(
            "INSERT INTO location_points (device_id, client_point_id, latitude, longitude, accuracy_m, " +
            "altitude_m, speed_mps, heading_deg, recorded_at, received_at, source, " +
            "district, city, region, country_code, country) VALUES ");
        var parameters = new List<NpgsqlParameter>(points.Count * ColumnsPerRow);

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (i > 0)
            {
                sql.Append(", ");
            }

            sql.Append('(');
            AppendParameter(sql, parameters, NpgsqlDbType.Uuid, p.DeviceId, first: true);
            AppendParameter(sql, parameters, NpgsqlDbType.Uuid, p.ClientPointId);
            AppendParameter(sql, parameters, NpgsqlDbType.Double, p.Latitude);
            AppendParameter(sql, parameters, NpgsqlDbType.Double, p.Longitude);
            AppendParameter(sql, parameters, NpgsqlDbType.Real, p.AccuracyM);
            AppendParameter(sql, parameters, NpgsqlDbType.Real, p.AltitudeM);
            AppendParameter(sql, parameters, NpgsqlDbType.Real, p.SpeedMps);
            AppendParameter(sql, parameters, NpgsqlDbType.Real, p.HeadingDeg);
            AppendParameter(sql, parameters, NpgsqlDbType.TimestampTz, p.RecordedAt);
            AppendParameter(sql, parameters, NpgsqlDbType.TimestampTz, p.ReceivedAt);
            AppendParameter(sql, parameters, NpgsqlDbType.Text, p.Source);
            AppendParameter(sql, parameters, NpgsqlDbType.Text, p.District);
            AppendParameter(sql, parameters, NpgsqlDbType.Text, p.City);
            AppendParameter(sql, parameters, NpgsqlDbType.Text, p.Region);
            AppendParameter(sql, parameters, NpgsqlDbType.Varchar, p.CountryCode);
            AppendParameter(sql, parameters, NpgsqlDbType.Text, p.Country);
            sql.Append(')');
        }

        sql.Append(" ON CONFLICT (device_id, client_point_id) DO NOTHING");

        return await db.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
    }

    public async Task<IReadOnlyList<LocationPoint>> GetForDeviceAsync(
        Guid deviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = db.LocationPoints.AsNoTracking().Where(p => p.DeviceId == deviceId);

        if (from is { } fromValue)
        {
            query = query.Where(p => p.RecordedAt >= fromValue);
        }

        if (to is { } toValue)
        {
            query = query.Where(p => p.RecordedAt <= toValue);
        }

        return await query
            .OrderByDescending(p => p.RecordedAt)
            .ThenByDescending(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<LocationPoint?> GetLatestAsync(Guid deviceId, CancellationToken cancellationToken = default) =>
        db.LocationPoints
            .AsNoTracking()
            .Where(p => p.DeviceId == deviceId)
            .OrderByDescending(p => p.RecordedAt)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> DeleteRecordedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        db.LocationPoints.Where(p => p.RecordedAt < cutoff).ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<PointCoordinates>> GetWithoutPlaceAsync(
        long afterId,
        int take,
        CancellationToken cancellationToken = default) =>
        await db.LocationPoints
            .AsNoTracking()
            .Where(p => p.Id > afterId && p.CountryCode == null)
            .OrderBy(p => p.Id)
            .Take(take)
            .Select(p => new PointCoordinates(p.Id, p.Latitude, p.Longitude))
            .ToListAsync(cancellationToken);

    public Task SetPlaceAsync(long id, PlaceMatch place, CancellationToken cancellationToken = default) =>
        db.LocationPoints
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.City, place.City)
                    .SetProperty(p => p.Region, place.Region)
                    .SetProperty(p => p.CountryCode, place.CountryCode)
                    .SetProperty(p => p.Country, place.Country),
                cancellationToken);

    private static void AppendParameter(
        StringBuilder sql,
        List<NpgsqlParameter> parameters,
        NpgsqlDbType type,
        object? value,
        bool first = false)
    {
        var name = $"p{parameters.Count}";
        parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
        if (!first)
        {
            sql.Append(", ");
        }

        sql.Append('@').Append(name);
    }
}
