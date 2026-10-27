namespace LocationTracker.Api.Auth;

public static class DeviceContext
{
    public const string DeviceIdHeader = "X-Device-Id";
    public const string DeviceTokenHeader = "X-Device-Token";

    private const string ItemKey = "LocationTracker.DeviceId";

    public static void SetDeviceId(this HttpContext context, Guid deviceId) => context.Items[ItemKey] = deviceId;

    public static Guid? GetDeviceIdOrNull(this HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is Guid id ? id : null;

    /// <summary>For endpoints marked with <see cref="RequireDeviceAttribute"/>, where the middleware has already run.</summary>
    public static Guid GetDeviceId(this HttpContext context) =>
        context.GetDeviceIdOrNull()
        ?? throw new InvalidOperationException("No authenticated device. Is the endpoint missing [RequireDevice]?");
}
