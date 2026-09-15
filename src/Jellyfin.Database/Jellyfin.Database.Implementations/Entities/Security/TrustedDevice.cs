using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS1591, SA1134, SA1516

namespace Jellyfin.Database.Implementations.Entities.Security;

/// <summary>A user-scoped installation credential that may bypass only a TOTP challenge.</summary>
public class TrustedDevice
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(64)] public string CredentialHash { get; set; } = string.Empty;
    [MaxLength(256)] public string InstallationId { get; set; } = string.Empty;
    [MaxLength(128)] public string FriendlyName { get; set; } = string.Empty;
    [MaxLength(64)] public string AppName { get; set; } = string.Empty;
    [MaxLength(32)] public string AppVersion { get; set; } = string.Empty;
    [MaxLength(64)] public string Platform { get; set; } = string.Empty;
    [MaxLength(64)] public string OsVersion { get; set; } = string.Empty;
    [MaxLength(64)] public string LastIpAddress { get; set; } = string.Empty;
    [MaxLength(32)] public string Source { get; set; } = "Observed";
    [MaxLength(32)] public string State { get; set; } = "Observed";
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DateTime? IssuedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the next login must complete direct 2FA after an idle logout.
    /// </summary>
    public bool RequiresFreshTwoFactor { get; set; }
}
