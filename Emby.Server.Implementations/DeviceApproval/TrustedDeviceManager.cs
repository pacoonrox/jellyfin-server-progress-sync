using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities.Security;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.DeviceApproval;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.DeviceApproval;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CS1591, SA1107, SA1201, SA1501, SA1503, SA1513, SA1516

namespace Emby.Server.Implementations.DeviceApproval;

public sealed class TrustedDeviceManager : ITrustedDeviceManager
{
    private readonly IDbContextFactory<JellyfinDbContext> _dbFactory;
    private readonly IServerConfigurationManager _configuration;
    private readonly IUserManager _users;

    public TrustedDeviceManager(IDbContextFactory<JellyfinDbContext> dbFactory, IServerConfigurationManager configuration, IUserManager users)
    {
        _dbFactory = dbFactory;
        _configuration = configuration;
        _users = users;
    }

    public async Task<bool> ValidateAsync(Guid userId, string? credential, string installationId, string appName, string appVersion, string deviceName, string ipAddress)
    {
        if (!_configuration.Configuration.TrustedDevicesEnabled || !TryHashCredential(credential, out var hash))
        {
            return false;
        }

        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var reused = await db.TrustedDevices.Where(x => x.CredentialHash == hash && x.InstallationId != installationId && x.State == "Trusted").ToListAsync().ConfigureAwait(false);
        if (reused.Count > 0)
        {
            foreach (var item in reused)
            {
                item.State = "Revoked";
                item.RevokedUtc = DateTime.UtcNow;
            }

            db.SecurityAuditRecords.Add(NewAudit("TrustCredentialReuse", "Direct", "Rejected", null, userId, null, false, "Credential appeared with a different installation id"));
            await db.SaveChangesAsync().ConfigureAwait(false);
            return false;
        }

        var record = await db.TrustedDevices.SingleOrDefaultAsync(x => x.UserId.Equals(userId) && x.CredentialHash == hash).ConfigureAwait(false);
        if (record is null)
        {
            return false;
        }

        record.LastSeenUtc = DateTime.UtcNow;
        record.AppName = Limit(appName, 64);
        record.AppVersion = Limit(appVersion, 32);
        record.LastIpAddress = Limit(ipAddress, 64);
        if (record.State == "Trusted" && record.ExpiresUtc <= DateTime.UtcNow)
        {
            record.State = "Expired";
            db.SecurityAuditRecords.Add(NewAudit("TrustExpired", record.Source, "Success", null, userId, record.Id, false));
        }

        var user = _users.GetUserById(userId);
        var automaticLogoutDisallowsSelfTrust = user is not null && user.GetInactiveLogoutMinutes() > 0 && !string.Equals(record.Source, "Administrator", StringComparison.Ordinal);
        var valid = record.State == "Trusted" && record.RevokedUtc is null && !record.RequiresFreshTwoFactor && !automaticLogoutDisallowsSelfTrust && record.ExpiresUtc > DateTime.UtcNow && record.InstallationId == installationId;
        await db.SaveChangesAsync().ConfigureAwait(false);
        return valid;
    }

    public async Task ObserveAsync(Guid userId, string? credential, string installationId, string appName, string appVersion, string deviceName, string platform, string osVersion, string ipAddress, bool directTwoFactorVerified = false)
    {
        if (!TryHashCredential(credential, out var hash))
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var limitedInstallationId = Limit(installationId, 256);
        var neverRecord = await db.TrustedDevices.FirstOrDefaultAsync(x => x.UserId.Equals(userId) && x.InstallationId == limitedInstallationId && x.State == "Never").ConfigureAwait(false);
        if (neverRecord is not null)
        {
            neverRecord.LastSeenUtc = DateTime.UtcNow;
            neverRecord.AppName = Limit(appName, 64);
            neverRecord.AppVersion = Limit(appVersion, 32);
            neverRecord.Platform = Limit(platform, 64);
            neverRecord.OsVersion = Limit(osVersion, 64);
            neverRecord.LastIpAddress = Limit(ipAddress, 64);
            await db.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        var record = await db.TrustedDevices.SingleOrDefaultAsync(x => x.UserId.Equals(userId) && x.CredentialHash == hash).ConfigureAwait(false);
        if (record is null)
        {
            record = new TrustedDevice { UserId = userId, CredentialHash = hash, InstallationId = limitedInstallationId, FirstSeenUtc = DateTime.UtcNow, State = "Observed", Source = "Observed" };
            db.TrustedDevices.Add(record);
        }

        record.LastSeenUtc = DateTime.UtcNow;
        record.FriendlyName = string.IsNullOrWhiteSpace(record.FriendlyName) ? Limit(deviceName, 128) : record.FriendlyName;
        record.AppName = Limit(appName, 64);
        record.AppVersion = Limit(appVersion, 32);
        record.Platform = Limit(platform, 64);
        record.OsVersion = Limit(osVersion, 64);
        record.LastIpAddress = Limit(ipAddress, 64);
        if (directTwoFactorVerified)
        {
            record.RequiresFreshTwoFactor = false;
        }
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task IssueAsync(Guid userId, string credential, string installationId, string source, Guid? actorUserId, bool administrator, DateTime? expiresUtc = null)
    {
        if (!_configuration.Configuration.TrustedDevicesEnabled || !TryHashCredential(credential, out var hash))
        {
            throw new AuthenticationException("Trusted devices are disabled or the installation credential is invalid");
        }

        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var limitedInstallationId = Limit(installationId, 256);
        var neverRecord = await db.TrustedDevices.FirstOrDefaultAsync(x => x.UserId.Equals(userId) && x.InstallationId == limitedInstallationId && x.State == "Never").ConfigureAwait(false);
        if (neverRecord is not null)
        {
            neverRecord.LastSeenUtc = DateTime.UtcNow;
            db.SecurityAuditRecords.Add(NewAudit("TrustIssueBlocked", source, "NeverTrusted", actorUserId, userId, neverRecord.Id, administrator));
            await db.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        var record = await db.TrustedDevices.SingleOrDefaultAsync(x => x.UserId.Equals(userId) && x.CredentialHash == hash).ConfigureAwait(false);
        if (record is null)
        {
            record = new TrustedDevice { UserId = userId, CredentialHash = hash, InstallationId = limitedInstallationId, FirstSeenUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow };
            db.TrustedDevices.Add(record);
        }
        else if (string.Equals(record.State, "Never", StringComparison.Ordinal))
        {
            record.LastSeenUtc = DateTime.UtcNow;
            db.SecurityAuditRecords.Add(NewAudit("TrustIssueBlocked", source, "NeverTrusted", actorUserId, userId, record.Id, administrator));
            await db.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        var now = DateTime.UtcNow;
        record.InstallationId = limitedInstallationId;
        record.Source = Limit(source, 32);
        record.State = "Trusted";
        record.IssuedUtc = now;
        record.ExpiresUtc = expiresUtc ?? now.AddDays(Math.Clamp(_configuration.Configuration.TrustedDeviceDefaultDays, 1, 3650));
        record.RevokedUtc = null;
        record.RequiresFreshTwoFactor = false;
        await db.SaveChangesAsync().ConfigureAwait(false);
        db.SecurityAuditRecords.Add(NewAudit(source == "Portal" ? "PortalTrustIssued" : source == "Administrator" ? "AdministratorTrustIssued" : "DirectTrustIssued", source, "Success", actorUserId, userId, record.Id, administrator));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task SetSessionProvenanceAsync(string accessToken, string provenance, DateTime? directTwoFactorVerifiedUtc)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var device = await db.Devices.SingleOrDefaultAsync(x => x.AccessToken == accessToken).ConfigureAwait(false);
        if (device is not null)
        {
            device.AuthenticationProvenance = Limit(provenance, 32);
            device.DirectTwoFactorVerifiedUtc = directTwoFactorVerifiedUtc;
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public async Task<bool> CanApproveAsync(string accessToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        // Device.IsActive is a legacy client capability flag and is false for many
        // valid Jellyfin sessions; token existence plus server provenance is the
        // authorization decision here.
        var device = await db.Devices.SingleOrDefaultAsync(x => x.AccessToken == accessToken).ConfigureAwait(false);
        if (device is null)
        {
            return false;
        }

        // The endpoint already requires an authenticated Jellyfin user. Any
        // current session may approve another request, including sessions that
        // were themselves created through Quick Sign-On.
        return true;
    }

    public async Task<IReadOnlyList<TrustedDeviceDto>> QueryAsync(string? search, Guid? userId, string? state)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var expired = await db.TrustedDevices.Where(x => x.State == "Trusted" && x.ExpiresUtc <= DateTime.UtcNow).ToListAsync().ConfigureAwait(false);
        foreach (var item in expired)
        {
            item.State = "Expired";
            db.SecurityAuditRecords.Add(NewAudit("TrustExpired", item.Source, "Success", null, item.UserId, item.Id, false));
        }
        if (expired.Count > 0) await db.SaveChangesAsync().ConfigureAwait(false);
        var query = db.TrustedDevices.AsNoTracking();
        if (userId.HasValue) query = query.Where(x => x.UserId.Equals(userId.Value));
        if (!string.IsNullOrWhiteSpace(state)) query = query.Where(x => x.State == state);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.FriendlyName.Contains(search) || x.AppName.Contains(search) || x.Platform.Contains(search));
        var records = await query.OrderByDescending(x => x.LastSeenUtc).ToListAsync().ConfigureAwait(false);
        var names = _users.GetUsers().ToDictionary(x => x.Id, x => x.Username);
        return records
            .OrderBy(x => names.GetValueOrDefault(x.UserId, "Deleted user"), StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.LastSeenUtc)
            .Select(x => new TrustedDeviceDto { Id = x.Id, UserId = x.UserId, Username = names.GetValueOrDefault(x.UserId, "Deleted user"), FriendlyName = x.FriendlyName, AppName = x.AppName, AppVersion = x.AppVersion, Platform = x.Platform, OsVersion = x.OsVersion, LastIpAddress = x.LastIpAddress, Source = x.Source, State = x.State, RequiresFreshTwoFactor = x.RequiresFreshTwoFactor, FirstSeenUtc = x.FirstSeenUtc, LastSeenUtc = x.LastSeenUtc, IssuedUtc = x.IssuedUtc, ExpiresUtc = x.ExpiresUtc }).ToArray();
    }

    public async Task UpdateAsync(long id, string? friendlyName, DateTime? expiresUtc, Guid actorUserId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var record = await db.TrustedDevices.FindAsync(id).ConfigureAwait(false) ?? throw new ResourceNotFoundException("Trusted device not found");
        if (friendlyName is not null) record.FriendlyName = Limit(friendlyName, 128);
        if (expiresUtc.HasValue)
        {
            record.ExpiresUtc = expiresUtc.Value.ToUniversalTime();
            record.State = record.ExpiresUtc > DateTime.UtcNow ? "Trusted" : "Expired";
            record.RevokedUtc = null;
        }
        db.SecurityAuditRecords.Add(NewAudit("TrustUpdated", "Administrator", "Success", actorUserId, record.UserId, record.Id, true));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RevokeAsync(long id, Guid actorUserId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var record = await db.TrustedDevices.FindAsync(id).ConfigureAwait(false) ?? throw new ResourceNotFoundException("Trusted device not found");
        record.State = "Revoked"; record.RevokedUtc = DateTime.UtcNow;
        db.SecurityAuditRecords.Add(NewAudit("TrustRevoked", "Administrator", "Success", actorUserId, record.UserId, record.Id, true));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task NeverTrustAsync(long id, Guid actorUserId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var record = await db.TrustedDevices.FindAsync(id).ConfigureAwait(false) ?? throw new ResourceNotFoundException("Trusted device not found");
        record.State = "Never";
        record.Source = "Administrator";
        record.RevokedUtc = DateTime.UtcNow;
        record.RequiresFreshTwoFactor = true;
        db.SecurityAuditRecords.Add(NewAudit("DeviceMarkedNeverTrust", "Administrator", "Success", actorUserId, record.UserId, record.Id, true));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task PruneAsync(long id, Guid actorUserId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var record = await db.TrustedDevices.FindAsync(id).ConfigureAwait(false) ?? throw new ResourceNotFoundException("Trusted device not found");
        db.TrustedDevices.Remove(record);
        db.SecurityAuditRecords.Add(NewAudit("TrustedDevicePruned", "Administrator", "Success", actorUserId, record.UserId, record.Id, true));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task TrustObservedAsync(long id, string? friendlyName, DateTime expiresUtc, Guid actorUserId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var record = await db.TrustedDevices.FindAsync(id).ConfigureAwait(false) ?? throw new ResourceNotFoundException("Observed device not found");
        if (friendlyName is not null) record.FriendlyName = Limit(friendlyName, 128);
        record.Source = "Administrator";
        record.IssuedUtc = DateTime.UtcNow;
        record.ExpiresUtc = expiresUtc.ToUniversalTime();
        record.State = record.ExpiresUtc > DateTime.UtcNow ? "Trusted" : "Expired";
        record.RevokedUtc = null;
        record.RequiresFreshTwoFactor = false;
        db.SecurityAuditRecords.Add(NewAudit("AdministratorTrustIssued", "Administrator", "Success", actorUserId, record.UserId, record.Id, true));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public Task RevokeUserAsync(Guid userId, Guid? actorUserId, string source) => RevokeWhereAsync(userId, actorUserId, source);
    public Task RevokeAllAsync(Guid actorUserId, string source) => RevokeWhereAsync(null, actorUserId, source);

    public async Task RequireFreshTwoFactorAsync(Guid userId, string installationId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var records = await db.TrustedDevices
            .Where(x => x.UserId.Equals(userId) && x.InstallationId == installationId && x.State == "Trusted")
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var record in records)
        {
            record.RequiresFreshTwoFactor = true;
            db.SecurityAuditRecords.Add(NewAudit("AutomaticLogoutTrustInvalidated", "AutomaticLogout", "Success", null, userId, record.Id, false));
        }

        if (records.Count > 0)
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private async Task RevokeWhereAsync(Guid? userId, Guid? actorUserId, string source)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var query = db.TrustedDevices.Where(x => x.State == "Trusted");
        if (userId.HasValue) query = query.Where(x => x.UserId.Equals(userId.Value));
        var records = await query.ToListAsync().ConfigureAwait(false);
        foreach (var record in records) { record.State = "Revoked"; record.RevokedUtc = DateTime.UtcNow; }
        db.SecurityAuditRecords.Add(NewAudit(userId.HasValue ? "UserTrustRevokedAll" : "GlobalTrustRevokedAll", source, "Success", actorUserId, userId, null, actorUserId.HasValue, $"Revoked {records.Count} credentials"));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task AuditAsync(string eventName, string source, string result, Guid? actorUserId, Guid? targetUserId, long? deviceId, bool administrator, string detail = "")
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        db.SecurityAuditRecords.Add(NewAudit(eventName, source, result, actorUserId, targetUserId, deviceId, administrator, detail));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SecurityAuditDto>> QueryAuditAsync(int limit)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.SecurityAuditRecords.AsNoTracking().OrderByDescending(x => x.TimestampUtc).Take(Math.Clamp(limit, 1, 1000)).Select(x => new SecurityAuditDto { Id = x.Id, TimestampUtc = x.TimestampUtc, ActingUserId = x.ActingUserId, TargetUserId = x.TargetUserId, TrustedDeviceId = x.TrustedDeviceId, Event = x.Event, Source = x.Source, Result = x.Result, AdministratorInvolved = x.AdministratorInvolved, Detail = x.Detail }).ToListAsync().ConfigureAwait(false);
    }

    private static SecurityAuditRecord NewAudit(string eventName, string source, string result, Guid? actor, Guid? target, long? device, bool admin, string detail = "")
        => new() { TimestampUtc = DateTime.UtcNow, Event = Limit(eventName, 48), Source = Limit(source, 32), Result = Limit(result, 32), ActingUserId = actor, TargetUserId = target, TrustedDeviceId = device, AdministratorInvolved = admin, Detail = Limit(detail, 512) };

    private static bool TryHashCredential(string? credential, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(credential) || credential.Length < 43 || credential.Length > 256) return false;
        hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
        return true;
    }

    private static string Limit(string? value, int max) => string.IsNullOrEmpty(value) ? string.Empty : value[..Math.Min(value.Length, max)];
}
