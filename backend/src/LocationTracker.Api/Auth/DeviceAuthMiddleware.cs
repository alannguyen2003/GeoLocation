using LocationTracker.Business.Security;
using LocationTracker.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocationTracker.Api.Auth;

/// <summary>
/// Authenticates endpoints marked with <see cref="RequireDeviceAttribute"/>: hashes the token from
/// X-Device-Token, asks <see cref="IDeviceService"/> to verify it and stores the device ID in HttpContext.Items.
/// </summary>
public class DeviceAuthMiddleware(RequestDelegate next)
{
    // Tokens are 43 characters; anything far longer is not worth hashing.
    private const int MaxTokenLength = 256;

    public async Task InvokeAsync(HttpContext context, IDeviceService devices, IProblemDetailsService problemDetails)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireDeviceAttribute>() is null)
        {
            await next(context);
            return;
        }

        var idHeader = context.Request.Headers[DeviceContext.DeviceIdHeader].ToString();
        var token = context.Request.Headers[DeviceContext.DeviceTokenHeader].ToString();

        var authenticated =
            Guid.TryParse(idHeader, out var deviceId)
            && token.Length is > 0 and <= MaxTokenLength
            && await devices.VerifyAsync(deviceId, DeviceTokens.Hash(token), context.RequestAborted);

        if (!authenticated)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unknown device or invalid device token.",
                },
            });
            return;
        }

        context.SetDeviceId(deviceId);
        await next(context);
    }
}
