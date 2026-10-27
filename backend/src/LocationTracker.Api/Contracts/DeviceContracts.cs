using System.ComponentModel.DataAnnotations;

namespace LocationTracker.Api.Contracts;

public class RegisterDeviceRequest
{
    [Required]
    [MaxLength(16)]
    public string? Platform { get; init; }
}

public record RegisterDeviceResponse(Guid DeviceId, string DeviceToken);
