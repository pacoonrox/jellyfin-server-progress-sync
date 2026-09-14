namespace Jellyfin.Api.Models.UserDtos;

/// <summary>
/// Request body containing a two-factor authentication code.
/// </summary>
public class VerifyTwoFactorRequest
{
    /// <summary>
    /// Gets or sets the two-factor authentication code.
    /// </summary>
    public string? Code { get; set; }
}
