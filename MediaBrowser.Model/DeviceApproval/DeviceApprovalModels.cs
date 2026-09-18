using System;
using System.Collections.Generic;
using Jellyfin.Database.Implementations.Enums;

#pragma warning disable CS1591, SA1136, SA1402, SA1502, SA1516, SA1649

namespace MediaBrowser.Model.DeviceApproval;

public enum DeviceApprovalState { Pending, Selected, Completing, Approved, Denied, Canceled, Expired }

public sealed class DeviceApprovalInitiateRequest
{
    public string DeviceCredential { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
}

public sealed class DeviceApprovalRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string? RequestSecret { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ConnectionDomain { get; set; } = string.Empty;
    public string? RequestingIpAddress { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DeviceApprovalState State { get; set; }
    public object? AuthenticationResult { get; set; }
    public bool TrustAllowed { get; set; }
    public int TrustDurationDays { get; set; } = 30;
}

public sealed class DeviceApprovalConfirmRequest
{
    public bool Matches { get; set; }
    public bool TrustDevice { get; set; }
}

public sealed class TrustedDeviceDto
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string LastIpAddress { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool RequiresFreshTwoFactor { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DateTime? IssuedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
}

public sealed class TrustedDevicePolicyDto
{
    public bool Enabled { get; set; }
    public int DefaultTrustDays { get; set; }
    public int InactiveLogoutMinutes { get; set; }
    public InactiveLogoutScope InactiveLogoutScope { get; set; }
}

public sealed class TrustedDeviceUpdateRequest
{
    public string? FriendlyName { get; set; }
    public DateTime? ExpiresUtc { get; set; }
}

public sealed class TrustedDeviceQueryResult
{
    public IReadOnlyList<TrustedDeviceDto> Items { get; set; } = Array.Empty<TrustedDeviceDto>();
}

public sealed class SecurityAuditDto
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public Guid? ActingUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public long? TrustedDeviceId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool AdministratorInvolved { get; set; }
    public string Detail { get; set; } = string.Empty;
}
