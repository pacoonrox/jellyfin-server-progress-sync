using System;
using System.ComponentModel.DataAnnotations;
using Jellyfin.Api.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.ProgressSync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Progress sync controller.
/// </summary>
[Route("ProgressSync")]
[Authorize(Policy = Policies.RequiresElevation)]
public class ProgressSyncController : BaseJellyfinApiController
{
    private readonly IProgressSyncManager _progressSyncManager;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressSyncController"/> class.
    /// </summary>
    public ProgressSyncController(IProgressSyncManager progressSyncManager, IUserManager userManager)
    {
        _progressSyncManager = progressSyncManager;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets synced users for a series.
    /// </summary>
    [HttpGet("Series/{seriesId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ProgressSyncSeriesDto> GetSeries([FromRoute, Required] Guid seriesId)
        => new ProgressSyncSeriesDto { SeriesId = seriesId, UserIds = _progressSyncManager.GetSeriesUsers(seriesId) };

    /// <summary>
    /// Adds a user to a series progress sync group.
    /// </summary>
    [HttpPost("Series/{seriesId}/Users/{targetUserId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ProgressSyncSeriesDto> AddSeriesUser([FromRoute, Required] Guid seriesId, [FromRoute, Required] Guid targetUserId)
    {
        var currentUser = _userManager.GetUserById(User.GetUserId());
        var targetUser = _userManager.GetUserById(targetUserId);
        if (currentUser is null || targetUser is null)
        {
            return NotFound();
        }

        return _progressSyncManager.AddUser(seriesId, currentUser.Id, targetUser.Id);
    }

    /// <summary>
    /// Removes a user from a series progress sync group.
    /// </summary>
    [HttpDelete("Series/{seriesId}/Users/{targetUserId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ProgressSyncSeriesDto> RemoveSeriesUser([FromRoute, Required] Guid seriesId, [FromRoute, Required] Guid targetUserId)
        => _progressSyncManager.RemoveUser(seriesId, targetUserId);
}
