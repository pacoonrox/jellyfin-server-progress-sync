using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS1591, SA1134, SA1516

namespace Jellyfin.Database.Implementations.Entities.Security;

/// <summary>Non-secret audit trail for device approval and trust policy changes.</summary>
public class SecurityAuditRecord
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public Guid? ActingUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public long? TrustedDeviceId { get; set; }
    [MaxLength(48)] public string Event { get; set; } = string.Empty;
    [MaxLength(32)] public string Source { get; set; } = string.Empty;
    [MaxLength(32)] public string Result { get; set; } = string.Empty;
    public bool AdministratorInvolved { get; set; }
    [MaxLength(512)] public string Detail { get; set; } = string.Empty;
}
