using LocationTracker.Api.Auth;
using LocationTracker.Api.Contracts;
using LocationTracker.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocationTracker.Api.Controllers;

[ApiController]
[Route("api/locations")]
[RequireDevice]
public class LocationsController(ILocationService locations) : ControllerBase
{
    // 100 points are roughly 25 KB of JSON; this leaves plenty of headroom.
    private const long MaxBatchBodyBytes = 256 * 1024;

    [HttpPost("batch")]
    [RequestSizeLimit(MaxBatchBodyBytes)]
    public async Task<ActionResult<BatchUploadResponse>> UploadBatch(
        LocationBatchRequest request,
        CancellationToken cancellationToken)
    {
        var points = request.Points?.Select(p => p.ToModel()).ToList();
        var result = await locations.UploadBatchAsync(HttpContext.GetDeviceId(), points, cancellationToken);
        return Ok(new BatchUploadResponse(result.Accepted, result.Duplicates));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationPointResponse>>> GetPoints(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var points = await locations.GetPointsAsync(HttpContext.GetDeviceId(), from, to, limit, cancellationToken);
        return Ok(points.Select(LocationPointResponse.From).ToList());
    }

    [HttpGet("latest")]
    public async Task<ActionResult<LocationPointResponse>> GetLatest(CancellationToken cancellationToken)
    {
        var point = await locations.GetLatestAsync(HttpContext.GetDeviceId(), cancellationToken);
        return point is null ? NoContent() : Ok(LocationPointResponse.From(point));
    }
}
