using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Model.DeviceApproval;

#pragma warning disable CS1591, SA1516

namespace MediaBrowser.Controller.DeviceApproval;

public interface ITrustedDeviceManager
{
    Task<bool> ValidateAsync(Guid userId, string? credential, string installationId, string appName, string appVersion, string deviceName, string ipAddress);
    Task ObserveAsync(Guid userId, string? credential, string installationId, string appName, string appVersion, string deviceName, string platform, string osVersion, string ipAddress, bool directTwoFactorVerified = false);
    Task IssueAsync(Guid userId, string credential, string installationId, string source, Guid? actorUserId, bool administrator, DateTime? expiresUtc = null);
    Task SetSessionProvenanceAsync(string accessToken, string provenance, DateTime? directTwoFactorVerifiedUtc);
    Task<bool> CanApproveAsync(string accessToken);
    Task<IReadOnlyList<TrustedDeviceDto>> QueryAsync(string? search, Guid? userId, string? state);
    Task UpdateAsync(long id, string? friendlyName, DateTime? expiresUtc, Guid actorUserId);
    Task TrustObservedAsync(long id, string? friendlyName, DateTime expiresUtc, Guid actorUserId);
    Task RevokeAsync(long id, Guid actorUserId);
    Task RevokeUserAsync(Guid userId, Guid? actorUserId, string source);
    Task RevokeAllAsync(Guid actorUserId, string source);
    Task RequireFreshTwoFactorAsync(Guid userId, string installationId);
    Task AuditAsync(string eventName, string source, string result, Guid? actorUserId, Guid? targetUserId, long? deviceId, bool administrator, string detail = "");
    Task<IReadOnlyList<SecurityAuditDto>> QueryAuditAsync(int limit);
}
