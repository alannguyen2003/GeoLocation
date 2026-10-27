using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

namespace LocationTracker.Api.Infrastructure;

public static class DatabaseConnection
{
    /// <summary>
    /// Uses DATABASE_URL (Render) when set, otherwise ConnectionStrings:Default (user secrets locally).
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        var url = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url))
        {
            return FromUrl(url);
        }

        return configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "No database configured. Set DATABASE_URL or ConnectionStrings:Default (e.g. with dotnet user-secrets).");
    }

    /// <summary>
    /// Converts a postgres:// or postgresql:// URI (the form Render provides) into the key-value form Npgsql expects.
    /// </summary>
    public static string FromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql"))
        {
            throw new InvalidOperationException("DATABASE_URL must be a postgres:// or postgresql:// URI.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            // Render's internal URL is on a private network and its external URL needs TLS.
            // Prefer uses TLS whenever the server offers it, so the same setting works for both.
            SslMode = SslMode.Prefer,
            // Neither Render nor the local container uses Kerberos. Without this, Npgsql probes for
            // GSS encryption and logs a missing libgssapi_krb5 error from the aspnet base image.
            GssEncryptionMode = GssEncryptionMode.Disable,
        };

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (query.TryGetValue("sslmode", out var sslMode)
            && Enum.TryParse<SslMode>(sslMode.ToString().Replace("-", string.Empty), ignoreCase: true, out var parsed))
        {
            builder.SslMode = parsed;
        }

        return builder.ConnectionString;
    }
}
