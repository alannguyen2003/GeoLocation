using LocationTracker.Api.Infrastructure;
using Npgsql;

namespace LocationTracker.Api.Tests;

public class DatabaseConnectionTests
{
    [Fact]
    public void FromUrl_RenderStyleUri_IsConvertedToKeyValueForm()
    {
        var result = new NpgsqlConnectionStringBuilder(
            DatabaseConnection.FromUrl("postgresql://tracker_user:p%40ss%3Aword@dpg-abc123-a:5432/tracker_db"));

        Assert.Equal("dpg-abc123-a", result.Host);
        Assert.Equal(5432, result.Port);
        Assert.Equal("tracker_db", result.Database);
        Assert.Equal("tracker_user", result.Username);
        Assert.Equal("p@ss:word", result.Password);
        Assert.Equal(SslMode.Prefer, result.SslMode);
    }

    [Fact]
    public void FromUrl_WithoutPort_DefaultsTo5432()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnection.FromUrl("postgres://u:p@host/db"));

        Assert.Equal(5432, result.Port);
    }

    [Fact]
    public void FromUrl_SslModeQuery_IsHonoured()
    {
        var result = new NpgsqlConnectionStringBuilder(
            DatabaseConnection.FromUrl("postgres://u:p@host.render.com/db?sslmode=require"));

        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void FromUrl_OtherScheme_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseConnection.FromUrl("mysql://u:p@host/db"));
    }
}
