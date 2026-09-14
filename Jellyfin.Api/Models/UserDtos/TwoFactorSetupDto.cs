namespace Jellyfin.Api.Models.UserDtos;

/// <summary>
/// Two-factor authentication setup information.
/// </summary>
public class TwoFactorSetupDto
{
    /// <summary>
    /// Gets or sets the manual entry key.
    /// </summary>
    public string? ManualEntryKey { get; set; }

    /// <summary>
    /// Gets or sets the otpauth URI for authenticator apps.
    /// </summary>
    public string? OtpAuthUri { get; set; }
}
