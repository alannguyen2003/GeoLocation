using LocationTracker.Api.Auth;
using LocationTracker.Api.Contracts;
using LocationTracker.Api.Infrastructure;
using LocationTracker.Business.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocationTracker.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController(IDeviceService devices) : ControllerBase
{
    /// <summary>Registers a device. The token is returned only in this response.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.RegistrationPolicy)]
    public async Task<ActionResult<RegisterDeviceResponse>> Register(
        RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var registration = await devices.RegisterAsync(request.Platform, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            new RegisterDeviceResponse(registration.DeviceId, registration.DeviceToken));
    }

    /// <summary>Deletes the calling device and every point it uploaded.</summary>
    [HttpDelete("me")]
    [RequireDevice]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        await devices.DeleteAsync(HttpContext.GetDeviceId(), cancellationToken);
        return NoContent();
    }
}
