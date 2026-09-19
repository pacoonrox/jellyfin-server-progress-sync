using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Helpers;
using Jellyfin.Data.Queries;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.DeviceApproval;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.DeviceApproval;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Api.Controllers;

#pragma warning disable CS1591, SA1107, SA1201, SA1210, SA1502, SA1503, SA1516

/// <summary>Shared queue and administrator-only trusted-device operations.</summary>
[Route("DeviceApproval")]
[Tags("Authentication")]
public sealed class DeviceApprovalController : BaseJellyfinApiController
{
    private readonly IDeviceApprovalPortal _portal;
    private readonly ITrustedDeviceManager _trust;
    private readonly IAuthorizationContext _authorization;
    private readonly IServerConfigurationManager _configuration;
    private readonly IDeviceManager _devices;
    private readonly ISessionManager _sessions;
    private readonly ILogger<DeviceApprovalController> _logger;

    public DeviceApprovalController(IDeviceApprovalPortal portal, ITrustedDeviceManager trust, IAuthorizationContext authorization, IServerConfigurationManager configuration, IDeviceManager devices, ISessionManager sessions, ILogger<DeviceApprovalController> logger)
    {
        _portal = portal; _trust = trust; _authorization = authorization; _configuration = configuration; _devices = devices; _sessions = sessions; _logger = logger;
    }

    [HttpGet("Enabled")]
    public ActionResult<bool> Enabled() => _portal.IsEnabled;

    [HttpPost("Requests")]
    public async Task<ActionResult<DeviceApprovalRequestDto>> Initiate([FromBody, Required] DeviceApprovalInitiateRequest request)
        => await _portal.InitiateAsync(await _authorization.GetAuthorizationInfo(Request).ConfigureAwait(false), request, HttpContext.GetNormalizedRemoteIP().ToString(), GetConnectionDomain(Request)).ConfigureAwait(false);

    [HttpGet("Requests/Status")]
    public ActionResult<DeviceApprovalRequestDto> Status([FromQuery, Required] string secret) => _portal.GetStatus(secret);

    [HttpDelete("Requests")]
    public async Task<ActionResult> Cancel([FromQuery, Required] string secret) { await _portal.CancelAsync(secret).ConfigureAwait(false); return NoContent(); }

    [HttpPost("Requests/Cancel")]
    public async Task<ActionResult> CancelWithBeacon([FromQuery, Required] string secret) { await _portal.CancelAsync(secret).ConfigureAwait(false); return NoContent(); }

    [HttpGet("Queue")]
    [Authorize]
    public ActionResult<IReadOnlyList<DeviceApprovalRequestDto>> Queue()
        => Ok(_portal.GetQueue(User.IsInRole(UserRoles.Administrator)));

    [HttpPost("Portal/Entered")]
    [Authorize]
    public async Task<ActionResult> PortalEntered()
    {
        var userId = User.GetUserId();
        try
        {
            await _trust.AuditAsync(
                "QuickConnectPortalEntered",
                "Portal",
                "Success",
                userId,
                userId,
                null,
                User.IsInRole(UserRoles.Administrator),
                $"IP: {HttpContext.GetNormalizedRemoteIP()}; Device: {User.GetDeviceId()}; Host: {Request.Host}; User-Agent: {Request.Headers.UserAgent}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to write the Quick Connect portal-entry security audit event for user {UserId}.", userId);
        }

        return NoContent();
    }

    [HttpPost("Queue/{requestId}/Select")]
    [Authorize]
    public async Task<ActionResult<DeviceApprovalRequestDto>> Select([FromRoute] string requestId, [FromQuery] Guid? userId = null)
        => Ok(await _portal.SelectAsync(requestId, RequestHelpers.GetUserId(User, userId), User.GetToken()!, User.GetIsApiKey()).ConfigureAwait(false));

    [HttpPost("Queue/{requestId}/Confirm")]
    [Authorize]
    public async Task<ActionResult> Confirm([FromRoute] string requestId, [FromBody] DeviceApprovalConfirmRequest request, [FromQuery] Guid? userId = null)
    {
        await _portal.ConfirmAsync(requestId, RequestHelpers.GetUserId(User, userId), User.GetToken()!, request.Matches, request.TrustDevice, User.GetIsApiKey()).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("Queue/{requestId}/Deny")]
    [Authorize]
    public async Task<ActionResult> Deny([FromRoute] string requestId, [FromQuery] Guid? userId = null) { await _portal.DenyAsync(requestId, RequestHelpers.GetUserId(User, userId)).ConfigureAwait(false); return NoContent(); }

    [HttpGet("Admin/Policy")]
    [Authorize(Roles = UserRoles.Administrator)]
    public ActionResult<TrustedDevicePolicyDto> Policy() => new TrustedDevicePolicyDto
    {
        Enabled = _configuration.Configuration.TrustedDevicesEnabled,
        DefaultTrustDays = _configuration.Configuration.TrustedDeviceDefaultDays
    };

    [HttpPut("Admin/Policy")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> UpdatePolicy([FromBody] TrustedDevicePolicyDto policy)
    {
        if (policy.DefaultTrustDays is < 1 or > 3650) return BadRequest("DefaultTrustDays must be between 1 and 3650");
        var oldEnabled = _configuration.Configuration.TrustedDevicesEnabled;
        _configuration.Configuration.TrustedDevicesEnabled = policy.Enabled;
        _configuration.Configuration.TrustedDeviceDefaultDays = policy.DefaultTrustDays;
        _configuration.SaveConfiguration();
        if (oldEnabled && !policy.Enabled) await _trust.RevokeAllAsync(User.GetUserId(), "PolicyDisabled").ConfigureAwait(false);
        await _trust.AuditAsync("TrustPolicyChanged", "Administrator", "Success", User.GetUserId(), null, null, true, $"Enabled={policy.Enabled}; Days={policy.DefaultTrustDays}").ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("Admin/Users/{userId:guid}/IdleLogoutPolicy")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult<IdleLogoutPolicyDto>> IdleLogoutPolicy([FromRoute] Guid userId)
        => Ok(await _trust.GetIdleLogoutPolicyAsync(userId).ConfigureAwait(false));

    [HttpPut("Admin/Users/{userId:guid}/IdleLogoutPolicy")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> UpdateIdleLogoutPolicy([FromRoute] Guid userId, [FromBody] IdleLogoutPolicyDto policy)
    {
        if (policy.Minutes is < 1 or > 525600) return BadRequest("Minutes must be between 1 and 525600");
        if (!Enum.IsDefined(policy.ScopeMode)) return BadRequest("ScopeMode is invalid");
        await _trust.SetIdleLogoutPolicyAsync(userId, policy, User.GetUserId()).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("Admin/TrustedDevices")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult<TrustedDeviceQueryResult>> Devices([FromQuery] string? search, [FromQuery] Guid? userId, [FromQuery] string? state)
        => new TrustedDeviceQueryResult { Items = await _trust.QueryAsync(search, userId, state).ConfigureAwait(false) };

    [HttpGet("Admin/Audit")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult<IReadOnlyList<SecurityAuditDto>>> Audit([FromQuery] int limit = 200)
        => Ok(await _trust.QueryAuditAsync(limit).ConfigureAwait(false));

    [HttpPut("Admin/TrustedDevices/{id:long}")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> Update([FromRoute] long id, [FromBody] TrustedDeviceUpdateRequest request) { await _trust.UpdateAsync(id, request.FriendlyName, request.ExpiresUtc, User.GetUserId()).ConfigureAwait(false); return NoContent(); }

    [HttpPost("Admin/TrustedDevices/{id:long}/Trust")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> Trust([FromRoute] long id, [FromBody] TrustedDeviceUpdateRequest request)
    {
        var record = (await _trust.QueryAsync(null, null, null).ConfigureAwait(false)).FirstOrDefault(x => x.Id == id);
        if (record is null) return NotFound();
        await _trust.TrustObservedAsync(id, request.FriendlyName, request.ExpiresUtc ?? DateTime.UtcNow.AddDays(_configuration.Configuration.TrustedDeviceDefaultDays), User.GetUserId()).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("Admin/TrustedDevices/{id:long}")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> Revoke([FromRoute] long id) { await _trust.RevokeAsync(id, User.GetUserId()).ConfigureAwait(false); return NoContent(); }

    [HttpPost("Admin/TrustedDevices/{id:long}/Never")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> NeverTrust([FromRoute] long id) { await _trust.NeverTrustAsync(id, User.GetUserId()).ConfigureAwait(false); return NoContent(); }

    [HttpDelete("Admin/TrustedDevices/{id:long}/Prune")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> Prune([FromRoute] long id) { await _trust.PruneAsync(id, User.GetUserId()).ConfigureAwait(false); return NoContent(); }

    [HttpDelete("Admin/Users/{userId:guid}/TrustedDevices")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> RevokeUser([FromRoute] Guid userId) { await _trust.RevokeUserAsync(userId, User.GetUserId(), "Administrator").ConfigureAwait(false); return NoContent(); }

    [HttpDelete("Admin/TrustedDevices")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> RevokeAll() { await _trust.RevokeAllAsync(User.GetUserId(), "Administrator").ConfigureAwait(false); return NoContent(); }

    [HttpPost("Admin/TrustedDevices/{id:long}/Logout")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> LogoutDevice([FromRoute] long id)
    {
        var record = (await _trust.QueryAsync(null, null, null).ConfigureAwait(false)).FirstOrDefault(x => x.Id == id);
        if (record is null)
        {
            return NotFound();
        }

        var devices = _devices.GetDevices(new DeviceQuery { UserId = record.UserId, DeviceId = record.DeviceId }).Items;
        foreach (var device in devices)
        {
            await _sessions.Logout(device).ConfigureAwait(false);
        }

        await _trust.RequireFreshTwoFactorAsync(record.UserId, record.DeviceId).ConfigureAwait(false);
        await _trust.AuditAsync("AdministratorDeviceLogout", "Administrator", "Success", User.GetUserId(), record.UserId, record.Id, true, $"Logged out {devices.Count} access token(s)").ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("Admin/Users/{userId:guid}/Logout")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> LogoutUserDevices([FromRoute] Guid userId)
    {
        var devices = _devices.GetDevices(new DeviceQuery { UserId = userId }).Items;
        foreach (var device in devices)
        {
            await _sessions.Logout(device).ConfigureAwait(false);
        }

        await _trust.RevokeUserAsync(userId, User.GetUserId(), "AdministratorLogout").ConfigureAwait(false);
        await _trust.AuditAsync("AdministratorUserLogout", "Administrator", "Success", User.GetUserId(), userId, null, true, $"Logged out {devices.Count} access token(s)").ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("Admin/Users/Logout")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> LogoutAllUsersDevices()
    {
        var devices = _devices.GetDevices(new DeviceQuery()).Items;
        foreach (var device in devices)
        {
            await _sessions.Logout(device).ConfigureAwait(false);
        }

        var actorUserId = User.GetUserId();
        await _trust.RevokeAllAsync(actorUserId, "AdministratorLogout").ConfigureAwait(false);
        await _trust.AuditAsync("AdministratorGlobalLogout", "Administrator", "Success", actorUserId, null, null, true, $"Logged out {devices.Count} access token(s)").ConfigureAwait(false);
        return NoContent();
    }

    private static string GetConnectionDomain(HttpRequest request)
    {
        var candidate = request.Headers["X-Forwarded-Host"].FirstOrDefault()
            ?? request.Headers["X-Original-Host"].FirstOrDefault()
            ?? request.Host.ToString();
        candidate = candidate.Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        if (Uri.TryCreate($"https://{candidate}", UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return request.Host.Host;
    }
}
