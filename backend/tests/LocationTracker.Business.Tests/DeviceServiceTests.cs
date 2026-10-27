using LocationTracker.Business.Security;
using LocationTracker.Business.Services;
using LocationTracker.Business.Tests.Fakes;
using LocationTracker.Business.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LocationTracker.Business.Tests;

public class DeviceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeDeviceRepository _repository = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly DeviceService _service;

    public DeviceServiceTests()
    {
        _service = new DeviceService(_repository, _time, NullLogger<DeviceService>.Instance);
    }

    [Theory]
    [InlineData("android", "android")]
    [InlineData("iOS", "ios")]
    [InlineData(" web ", "web")]
    public async Task Register_KnownPlatform_StoresNormalizedPlatform(string platform, string expected)
    {
        var registration = await _service.RegisterAsync(platform);

        Assert.Equal(expected, _repository.Devices[registration.DeviceId].Platform);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("windows")]
    public async Task Register_UnknownPlatform_IsRejected(string? platform)
    {
        var ex = await Assert.ThrowsAsync<BusinessValidationException>(() => _service.RegisterAsync(platform));

        Assert.Contains("platform", ex.Errors.Keys);
        Assert.Empty(_repository.Devices);
    }

    [Fact]
    public async Task Register_StoresOnlyTheTokenHash()
    {
        var registration = await _service.RegisterAsync("android");

        var stored = _repository.Devices[registration.DeviceId];
        Assert.NotEqual(registration.DeviceToken, stored.TokenHash);
        Assert.Equal(DeviceTokens.Hash(registration.DeviceToken), stored.TokenHash);
    }

    [Fact]
    public async Task Register_IssuesDifferentTokensEachTime()
    {
        var first = await _service.RegisterAsync("android");
        var second = await _service.RegisterAsync("android");

        Assert.NotEqual(first.DeviceId, second.DeviceId);
        Assert.NotEqual(first.DeviceToken, second.DeviceToken);
    }

    [Fact]
    public async Task Verify_CorrectToken_Succeeds()
    {
        var registration = await _service.RegisterAsync("ios");

        Assert.True(await _service.VerifyAsync(registration.DeviceId, DeviceTokens.Hash(registration.DeviceToken)));
    }

    [Fact]
    public async Task Verify_WrongToken_Fails()
    {
        var registration = await _service.RegisterAsync("ios");

        Assert.False(await _service.VerifyAsync(registration.DeviceId, DeviceTokens.Hash("not-the-token")));
    }

    [Fact]
    public async Task Verify_RawTokenInsteadOfHash_Fails()
    {
        var registration = await _service.RegisterAsync("ios");

        Assert.False(await _service.VerifyAsync(registration.DeviceId, registration.DeviceToken));
    }

    [Fact]
    public async Task Verify_UnknownDevice_Fails()
    {
        var registration = await _service.RegisterAsync("ios");

        Assert.False(await _service.VerifyAsync(Guid.NewGuid(), DeviceTokens.Hash(registration.DeviceToken)));
    }

    [Fact]
    public async Task Verify_AfterAWhile_UpdatesLastSeen()
    {
        var registration = await _service.RegisterAsync("web");
        _time.Advance(TimeSpan.FromHours(1));

        await _service.VerifyAsync(registration.DeviceId, DeviceTokens.Hash(registration.DeviceToken));

        Assert.Equal(Now.AddHours(1), _repository.Devices[registration.DeviceId].LastSeenAt);
    }

    [Fact]
    public async Task Delete_RemovesDevice()
    {
        var registration = await _service.RegisterAsync("web");

        Assert.True(await _service.DeleteAsync(registration.DeviceId));
        Assert.False(await _service.VerifyAsync(registration.DeviceId, DeviceTokens.Hash(registration.DeviceToken)));
    }
}
