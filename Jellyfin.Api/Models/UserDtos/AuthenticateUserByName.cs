namespace Jellyfin.Api.Models.UserDtos;

/// <summary>
/// The authenticate user by name request body.
/// </summary>
public class AuthenticateUserByName
{
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the plain text password.
    /// </summary>
    public string? Pw { get; set; }

    /// <summary>
    /// Gets or sets the two-factor authentication code.
    /// </summary>
    public string? TwoFactorCode { get; set; }

    /// <summary>Gets or sets the installation-specific random device credential.</summary>
    public string? DeviceCredential { get; set; }

    /// <summary>Gets or sets a value indicating whether this installation should be trusted after successful 2FA.</summary>
    public bool TrustDevice { get; set; }

    /// <summary>Gets or sets descriptive platform telemetry; it is never used as a credential.</summary>
    public string? Platform { get; set; }

    /// <summary>Gets or sets descriptive operating-system telemetry; it is never used as a credential.</summary>
    public string? OsVersion { get; set; }
}
