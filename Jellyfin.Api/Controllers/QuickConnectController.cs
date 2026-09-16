using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Jellyfin.Api.Helpers;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.QuickConnect;
using MediaBrowser.Model.QuickConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Jellyfin.Api.Controllers;

/// <summary>Legacy Quick Connect compatibility for native Jellyfin clients.</summary>
[Route("QuickConnect")]
[Tags("Authentication")]
public sealed class QuickConnectController : BaseJellyfinApiController
{
    private readonly IQuickConnect _quickConnect;
    private readonly IAuthorizationContext _authorization;

    public QuickConnectController(IQuickConnect quickConnect, IAuthorizationContext authorization)
    {
        _quickConnect = quickConnect;
        _authorization = authorization;
    }

    [HttpGet("Enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<bool> Enabled() => _quickConnect.IsEnabled;

    [HttpPost("Initiate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<QuickConnectResult>> Initiate()
    {
        try
        {
            return _quickConnect.TryConnect(await _authorization.GetAuthorizationInfo(Request).ConfigureAwait(false));
        }
        catch (AuthenticationException)
        {
            return Unauthorized("Quick Connect is disabled");
        }
    }

    [HttpGet("Connect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<QuickConnectResult> Connect([FromQuery, Required] string secret)
    {
        try
        {
            return _quickConnect.CheckRequestStatus(secret);
        }
        catch (ResourceNotFoundException)
        {
            return NotFound("Unknown secret");
        }
        catch (AuthenticationException)
        {
            return Unauthorized("Quick Connect is disabled");
        }
    }

    [HttpPost("Authorize")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<bool>> Authorize([FromQuery, Required] string code, [FromQuery] Guid? userId = null)
    {
        var authorizedUserId = RequestHelpers.GetUserId(User, userId);
        try
        {
            return await _quickConnect.AuthorizeRequest(authorizedUserId, code).ConfigureAwait(false);
        }
        catch (AuthenticationException)
        {
            return Unauthorized("Quick Connect is disabled");
        }
    }
}
