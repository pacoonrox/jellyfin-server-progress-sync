using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS1591, SA1134, SA1516

namespace Jellyfin.Database.Implementations.Entities;

/// <summary>An explicit per-device idle-logout override for the SelectedManual scope mode.</summary>
public class IdleLogoutDeviceOverride
{
    public IdleLogoutDeviceOverride(Guid userId, string deviceId, bool subject)
    {
        UserId = userId;
        DeviceId = deviceId;
        Subject = subject;
    }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public Guid UserId { get; private set; }

    [MaxLength(256)]
    public string DeviceId { get; private set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this device is subject to (true) or exempt from (false) idle logout.</summary>
    public bool Subject { get; set; }
}
