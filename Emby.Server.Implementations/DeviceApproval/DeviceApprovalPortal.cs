using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.DeviceApproval;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.DeviceApproval;

#pragma warning disable CS1591, CA1001, SA1107, SA1201, SA1306, SA1401, SA1501, SA1502, SA1503, SA1513, SA1516

namespace Emby.Server.Implementations.DeviceApproval;

public sealed class DeviceApprovalPortal : IDeviceApprovalPortal
{
    private readonly ConcurrentDictionary<string, Pending> _requests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _secretIndex = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServerConfigurationManager _configuration;
    private readonly ITrustedDeviceManager _trust;
    private readonly ISessionManager _sessions;
    private readonly IUserManager _users;
    private readonly TimeProvider _time;

    public DeviceApprovalPortal(IServerConfigurationManager configuration, ITrustedDeviceManager trust, ISessionManager sessions, IUserManager users, TimeProvider time)
    {
        _configuration = configuration; _trust = trust; _sessions = sessions; _users = users; _time = time;
    }

    public bool IsEnabled => _configuration.Configuration.DeviceApprovalAvailable;

    public async Task<DeviceApprovalRequestDto> InitiateAsync(AuthorizationInfo client, DeviceApprovalInitiateRequest request, string ipAddress, string connectionDomain)
    {
        AssertEnabled();
        ArgumentException.ThrowIfNullOrEmpty(client.DeviceId);
        ArgumentException.ThrowIfNullOrEmpty(client.Device);
        ArgumentException.ThrowIfNullOrEmpty(client.Client);
        ArgumentException.ThrowIfNullOrEmpty(client.Version);
        if (string.IsNullOrWhiteSpace(request.DeviceCredential) || request.DeviceCredential.Length < 43) throw new ArgumentException("A secure installation credential is required", nameof(request));
        Expire();
        await _gate.WaitAsync().ConfigureAwait(false);
        Pending pending;
        try
        {
            if (_requests.Count >= 100 || _requests.Values.Count(x => x.IpAddress == ipAddress) >= 5) throw new AuthenticationException("Too many pending device-approval requests");
            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var now = _time.GetUtcNow().UtcDateTime;
            pending = new Pending { Id = id, Secret = secret, DeviceCredential = request.DeviceCredential, InstallationId = client.DeviceId, DeviceName = client.Device, AppName = client.Client, AppVersion = client.Version, Platform = request.Platform, OsVersion = request.OsVersion, ConnectionDomain = connectionDomain, IpAddress = ipAddress, CreatedUtc = now, LastSeenUtc = now, ExpiresUtc = now.AddMinutes(5) };
            _requests[id] = pending; _secretIndex[secret] = id;
        }
        finally { _gate.Release(); }
        await _trust.AuditAsync("PortalQueueCreated", "Portal", "Success", null, null, null, false, $"Request {pending.Id}; {client.Client}; {client.Device}").ConfigureAwait(false);
        return ToDto(pending, false, true);
    }

    public DeviceApprovalRequestDto GetStatus(string requestSecret)
    {
        AssertEnabled(); Expire();
        if (!_secretIndex.TryGetValue(requestSecret, out var id) || !_requests.TryGetValue(id, out var request)) throw new ResourceNotFoundException("Unknown or consumed request");
        lock (request)
        {
            request.LastSeenUtc = _time.GetUtcNow().UtcDateTime;
            var dto = ToDto(request, false, true);
            if (request.State == DeviceApprovalState.Approved)
            {
                dto.AuthenticationResult = request.Result;
                request.Result = null;
                _secretIndex.TryRemove(requestSecret, out _);
                _requests.TryRemove(id, out _);
            }
            return dto;
        }
    }

    public IReadOnlyList<DeviceApprovalRequestDto> GetQueue(bool includeIpAddress)
    {
        AssertEnabled(); Expire();
        return _requests.Values.Where(x => x.State is DeviceApprovalState.Pending or DeviceApprovalState.Selected).OrderBy(x => x.CreatedUtc).Select(x => ToDto(x, includeIpAddress, false)).ToArray();
    }

    public async Task<DeviceApprovalRequestDto> SelectAsync(string requestId, Guid actorUserId, string actorAccessToken)
    {
        AssertEnabled(); Expire();
        if (!await _trust.CanApproveAsync(actorAccessToken).ConfigureAwait(false))
        {
            await _trust.AuditAsync("PortalSelection", "Portal", "RejectedProvenance", actorUserId, null, null, false).ConfigureAwait(false);
            throw new AuthenticationException("This session is not eligible to approve another device");
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var request = GetPending(requestId);
            if (request.State != DeviceApprovalState.Pending) throw new InvalidOperationException("Request is already selected or complete");
            request.State = DeviceApprovalState.Selected; request.SelectedBy = actorUserId; request.SelectedUtc = _time.GetUtcNow().UtcDateTime;
            var dto = ToDto(request, false, false);
            var user = _users.GetUserById(actorUserId);
            dto.TrustAllowed = _configuration.Configuration.TrustedDevicesEnabled && user is not null && !user.IsIdleLogoutEnabled();
            dto.TrustDurationDays = _configuration.Configuration.TrustedDeviceDefaultDays;
            return dto;
        }
        finally { _gate.Release(); }
    }

    public async Task<AuthenticationResult?> ConfirmAsync(string requestId, Guid actorUserId, string actorAccessToken, bool matches, bool trustDevice)
    {
        AssertEnabled(); Expire();
        if (!await _trust.CanApproveAsync(actorAccessToken).ConfigureAwait(false)) throw new AuthenticationException("Session is not eligible to approve devices");
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var request = GetPending(requestId);
            if (request.State != DeviceApprovalState.Selected || !request.SelectedBy.Equals(actorUserId) || request.SelectedUtc < _time.GetUtcNow().UtcDateTime.AddMinutes(-1))
            {
                await _trust.AuditAsync("PortalApproval", "Portal", "Conflict", actorUserId, null, null, false, $"Request {requestId}").ConfigureAwait(false);
                throw new InvalidOperationException("Selection is stale, belongs to another user, or is no longer pending");
            }

            if (!matches)
            {
                request.State = DeviceApprovalState.Pending; request.SelectedBy = null; request.SelectedUtc = null;
                await _trust.AuditAsync("PortalSelectionCanceled", "Portal", "Success", actorUserId, null, null, false, $"Request {requestId}").ConfigureAwait(false);
                return null;
            }

            request.State = DeviceApprovalState.Completing;
            var user = _users.GetUserById(actorUserId) ?? throw new AuthenticationException("Approving user no longer exists");
            if (user.HasPermission(Jellyfin.Database.Implementations.Enums.PermissionKind.IsDisabled))
            {
                request.State = DeviceApprovalState.Pending;
                request.SelectedBy = null;
                request.SelectedUtc = null;
                await _trust.AuditAsync("PortalApproval", "Portal", "RejectedDisabledAccount", actorUserId, actorUserId, null, user.HasPermission(Jellyfin.Database.Implementations.Enums.PermissionKind.IsAdministrator), $"Request {requestId}").ConfigureAwait(false);
                throw new AuthenticationException("The approving account is disabled");
            }

            AuthenticationResult? result = null;
            try
            {
                result = await _sessions.AuthenticatePortalSession(new AuthenticationRequest { UserId = actorUserId, DeviceId = request.InstallationId, DeviceName = request.DeviceName, App = request.AppName, AppVersion = request.AppVersion, DeviceCredential = request.DeviceCredential, Platform = request.Platform, OsVersion = request.OsVersion, RemoteEndPoint = request.IpAddress }).ConfigureAwait(false);
                // A successful portal approval is an explicit authorization by
                // the approving session. Automatically trust the requesting
                // installation for the configured period when the account is
                // eligible; while idle logout is enabled for the approving user,
                // trust issuance remains administrator-only.
                if (trustDevice && DeviceInventoryPolicy.ShouldTrack(request.AppName) && _configuration.Configuration.TrustedDevicesEnabled && !user.IsIdleLogoutEnabled())
                {
                    await _trust.IssueAsync(actorUserId, request.DeviceCredential, request.InstallationId, "Portal", actorUserId, user.HasPermission(Jellyfin.Database.Implementations.Enums.PermissionKind.IsAdministrator)).ConfigureAwait(false);
                }
                request.Result = result; request.State = DeviceApprovalState.Approved;
                await _trust.AuditAsync("PortalApproved", "Portal", "Success", actorUserId, actorUserId, null, user.HasPermission(Jellyfin.Database.Implementations.Enums.PermissionKind.IsAdministrator), $"Request {requestId}").ConfigureAwait(false);
                return result;
            }
            catch
            {
                if (result is not null)
                {
                    try
                    {
                        await _sessions.Logout(result.AccessToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Preserve the original approval failure; the token remains undisclosed.
                    }
                }
                request.State = DeviceApprovalState.Pending;
                request.SelectedBy = null;
                request.SelectedUtc = null;
                await _trust.AuditAsync("PortalApproval", "Portal", "Failed", actorUserId, actorUserId, null, user.HasPermission(Jellyfin.Database.Implementations.Enums.PermissionKind.IsAdministrator), $"Request {requestId}").ConfigureAwait(false);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task CancelAsync(string requestSecret)
    {
        if (_secretIndex.TryRemove(requestSecret, out var id) && _requests.TryRemove(id, out var request))
        {
            request.State = DeviceApprovalState.Canceled;
            await _trust.AuditAsync("PortalCanceled", "Portal", "Success", null, null, null, false, $"Request {id}").ConfigureAwait(false);
        }
    }

    public async Task DenyAsync(string requestId, Guid actorUserId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { var request = GetPending(requestId); if (!request.SelectedBy.Equals(actorUserId)) throw new AuthenticationException("Only the selecting user may deny this attempt"); request.State = DeviceApprovalState.Denied; _requests.TryRemove(requestId, out _); _secretIndex.TryRemove(request.Secret, out _); }
        finally { _gate.Release(); }
        await _trust.AuditAsync("PortalDenied", "Portal", "Success", actorUserId, null, null, false, $"Request {requestId}").ConfigureAwait(false);
    }

    private Pending GetPending(string id) => _requests.TryGetValue(id, out var value) && value.ExpiresUtc > _time.GetUtcNow().UtcDateTime ? value : throw new ResourceNotFoundException("Pending request not found");
    private void AssertEnabled() { if (!IsEnabled) throw new AuthenticationException("Device approval is disabled"); }
    private void Expire()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var pair in _requests.Where(x => x.Value.ExpiresUtc <= now || x.Value.LastSeenUtc.AddSeconds(10) <= now).ToArray())
        {
            var disconnected = pair.Value.LastSeenUtc.AddSeconds(10) <= now && pair.Value.ExpiresUtc > now;
            pair.Value.State = DeviceApprovalState.Expired; _requests.TryRemove(pair.Key, out _); _secretIndex.TryRemove(pair.Value.Secret, out _);
            _ = _trust.AuditAsync(disconnected ? "PortalDisconnected" : "PortalExpired", "Portal", "Success", null, null, null, false, $"Request {pair.Key}");
        }
    }
    private static DeviceApprovalRequestDto ToDto(Pending x, bool includeIp, bool requester) => new() { Id = x.Id, RequestSecret = requester ? x.Secret : null, DeviceName = x.DeviceName, AppName = x.AppName, AppVersion = x.AppVersion, Platform = x.Platform, OsVersion = x.OsVersion, ConnectionDomain = x.ConnectionDomain, RequestingIpAddress = includeIp ? x.IpAddress : null, CreatedUtc = x.CreatedUtc, ExpiresUtc = x.ExpiresUtc, State = x.State };

    private sealed class Pending
    {
        public string Id = string.Empty; public string Secret = string.Empty; public string DeviceCredential = string.Empty; public string InstallationId = string.Empty; public string DeviceName = string.Empty; public string AppName = string.Empty; public string AppVersion = string.Empty; public string Platform = string.Empty; public string OsVersion = string.Empty; public string ConnectionDomain = string.Empty; public string IpAddress = string.Empty; public DateTime CreatedUtc; public DateTime LastSeenUtc; public DateTime ExpiresUtc; public DeviceApprovalState State; public Guid? SelectedBy; public DateTime? SelectedUtc; public AuthenticationResult? Result;
    }
}
