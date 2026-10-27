namespace LocationTracker.Business.Models;

public static class LocationSource
{
    public const string Foreground = "foreground";
    public const string Background = "background";
    public const string Web = "web";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Foreground, Background, Web };
}
