namespace LocationTracker.Business.Models;

public static class DevicePlatform
{
    public const string Android = "android";
    public const string Ios = "ios";
    public const string Web = "web";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Android, Ios, Web };
}
