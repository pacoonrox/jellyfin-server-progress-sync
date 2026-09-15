using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Helpers;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.DeviceApproval;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.DeviceApproval;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    public DeviceApprovalController(IDeviceApprovalPortal portal, ITrustedDeviceManager trust, IAuthorizationContext authorization, IServerConfigurationManager configuration)
    {
        _portal = portal; _trust = trust; _authorization = authorization; _configuration = configuration;
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
        => Ok(_portal.GetQueue(User.IsInRole(UserRoles.Administrator), User.GetUserId()));

    [HttpPost("Queue/{requestId}/Select")]
    [Authorize]
    public async Task<ActionResult<DeviceApprovalRequestDto>> Select([FromRoute] string requestId)
        => Ok(await _portal.SelectAsync(requestId, User.GetUserId(), User.GetToken()!).ConfigureAwait(false));

    [HttpPost("Queue/{requestId}/Confirm")]
    [Authorize]
    public async Task<ActionResult> Confirm([FromRoute] string requestId, [FromBody] DeviceApprovalConfirmRequest request)
    {
        await _portal.ConfirmAsync(requestId, User.GetUserId(), User.GetToken()!, request.Matches, request.TrustDevice).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("Queue/{requestId}/Deny")]
    [Authorize]
    public async Task<ActionResult> Deny([FromRoute] string requestId) { await _portal.DenyAsync(requestId, User.GetUserId()).ConfigureAwait(false); return NoContent(); }

    [HttpGet("Admin/Policy")]
    [Authorize(Roles = UserRoles.Administrator)]
    public ActionResult<TrustedDevicePolicyDto> Policy() => new TrustedDevicePolicyDto { Enabled = _configuration.Configuration.TrustedDevicesEnabled, DefaultTrustDays = _configuration.Configuration.TrustedDeviceDefaultDays };

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

    [HttpDelete("Admin/TrustedDevices/{id:long}/Prune")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> Prune([FromRoute] long id) { await _trust.PruneAsync(id, User.GetUserId()).ConfigureAwait(false); return NoContent(); }

    [HttpDelete("Admin/Users/{userId:guid}/TrustedDevices")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> RevokeUser([FromRoute] Guid userId) { await _trust.RevokeUserAsync(userId, User.GetUserId(), "Administrator").ConfigureAwait(false); return NoContent(); }

    [HttpDelete("Admin/TrustedDevices")]
    [Authorize(Roles = UserRoles.Administrator)]
    public async Task<ActionResult> RevokeAll() { await _trust.RevokeAllAsync(User.GetUserId(), "Administrator").ConfigureAwait(false); return NoContent(); }

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
