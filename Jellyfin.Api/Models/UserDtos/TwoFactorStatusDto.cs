using System;
using Jellyfin.Database.Implementations.Enums;

namespace Jellyfin.Api.Models.UserDtos;

/// <summary>
/// Two-factor authentication status for a user.
/// </summary>
public class TwoFactorStatusDto
{
    /// <summary>
    /// Gets or sets the configured two-factor authentication policy.
    /// </summary>
    public TwoFactorAuthenticationPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether two-factor authentication is registered.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the registration timestamp.
    /// </summary>
    public DateTimeOffset? RegisteredDate { get; set; }

    /// <summary>
    /// Gets or sets the failed two-factor authentication attempt count.
    /// </summary>
    public int FailedAttemptCount { get; set; }
}
