namespace LocationTracker.Api.Auth;

/// <summary>Marks a controller or action as needing valid X-Device-Id / X-Device-Token headers.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireDeviceAttribute : Attribute;
