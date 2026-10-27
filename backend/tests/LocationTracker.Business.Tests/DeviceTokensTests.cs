using LocationTracker.Business.Security;

namespace LocationTracker.Business.Tests;

public class DeviceTokensTests
{
    [Fact]
    public void Generate_ReturnsUrlSafeTokenWith256BitsOfEntropy()
    {
        var token = DeviceTokens.Generate();

        Assert.Equal(43, token.Length);
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void Hash_IsDeterministicSha256Hex()
    {
        // Known SHA-256 of "abc".
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", DeviceTokens.Hash("abc"));
    }

    [Fact]
    public void HashesEqual_ComparesExactly()
    {
        var hash = DeviceTokens.Hash("token");

        Assert.True(DeviceTokens.HashesEqual(hash, DeviceTokens.Hash("token")));
        Assert.False(DeviceTokens.HashesEqual(hash, DeviceTokens.Hash("token2")));
        Assert.False(DeviceTokens.HashesEqual(hash, hash[..^1]));
    }
}
