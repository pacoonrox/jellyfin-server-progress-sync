#nullable disable

using MediaBrowser.Model.Dto;

namespace MediaBrowser.Controller.Authentication;

/// <summary>
/// A class representing an authentication result.
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public UserDto User { get; set; }

    /// <summary>
    /// Gets or sets the session info.
    /// </summary>
    public SessionInfoDto SessionInfo { get; set; }

    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the server id.
    /// </summary>
    public string ServerId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a two-factor authentication code is required before a session token can be issued.
    /// </summary>
    public bool RequiresTwoFactorAuthentication { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether two-factor authentication setup is required before a session token can be issued.
    /// </summary>
    public bool RequiresTwoFactorSetup { get; set; }

    /// <summary>Gets or sets a value indicating whether this direct 2FA attempt may issue device trust.</summary>
    public bool CanTrustDevice { get; set; }

    /// <summary>Gets or sets the configured default trust duration shown to the client.</summary>
    public int TrustedDeviceDefaultDays { get; set; }
}
