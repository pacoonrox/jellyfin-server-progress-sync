using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Jellyfin.Api.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Reviews controller for item ratings and comments.
/// </summary>
[Route("Reviews")]
[Authorize]
public class ReviewsController : BaseJellyfinApiController
{
    private readonly IReviewsManager _reviewsManager;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewsController"/> class.
    /// </summary>
    /// <param name="reviewsManager">The reviews manager.</param>
    /// <param name="userManager">The user manager.</param>
    public ReviewsController(IReviewsManager reviewsManager, IUserManager userManager)
    {
        _reviewsManager = reviewsManager;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets every review for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The item's reviews.</returns>
    [HttpGet("Items/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ReviewDto>> GetReviews([FromRoute, Required] Guid itemId)
        => Ok(_reviewsManager.GetReviews(itemId));

    /// <summary>
    /// Gets the aggregate rating summary for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The rating summary.</returns>
    [HttpGet("Items/{itemId}/Summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ItemRatingSummaryDto> GetSummary([FromRoute, Required] Guid itemId)
        => _reviewsManager.GetSummary(itemId);

    /// <summary>
    /// Gets aggregate rating summaries for several items at once.
    /// </summary>
    /// <param name="itemIds">A comma-delimited list of item ids.</param>
    /// <returns>A summary per item that has at least one review.</returns>
    [HttpGet("Summaries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyDictionary<Guid, ItemRatingSummaryDto>> GetSummaries([FromQuery, Required] string itemIds)
    {
        var ids = itemIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .ToArray();

        return Ok(_reviewsManager.GetSummaries(ids));
    }

    /// <summary>
    /// Gets the items with the highest average rating.
    /// </summary>
    /// <param name="minRatingCount">The minimum number of ratings an item must have.</param>
    /// <param name="limit">The maximum number of results.</param>
    /// <returns>The top rated items.</returns>
    [HttpGet("TopRated")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ItemRatingSummaryDto>> GetTopRated([FromQuery] int minRatingCount = 1, [FromQuery] int limit = 50)
        => Ok(_reviewsManager.GetTopRated(minRatingCount, limit));

    /// <summary>
    /// Gets the calling user's own review for an item, if any.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The user's review, or 404 if they have not reviewed the item.</returns>
    [HttpGet("Items/{itemId}/Mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReviewDto> GetMyReview([FromRoute, Required] Guid itemId)
    {
        var review = _reviewsManager.GetReview(itemId, User.GetUserId());
        return review is null ? NotFound() : review;
    }

    /// <summary>
    /// Gets every review the calling user has written.
    /// </summary>
    /// <returns>The user's reviews across all items.</returns>
    [HttpGet("Mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ReviewDto>> GetMyReviews()
        => Ok(_reviewsManager.GetReviewsByUser(User.GetUserId()));

    /// <summary>
    /// Creates or updates the calling user's review for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="request">The review contents.</param>
    /// <returns>The saved review.</returns>
    [HttpPost("Items/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReviewDto> UpdateReview([FromRoute, Required] Guid itemId, [FromBody, Required] UpdateReviewRequest request)
    {
        var userId = User.GetUserId();
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return NotFound();
        }

        return _reviewsManager.UpsertReview(itemId, userId, user.Username, request.Rating, request.Comment, request.ContainsSpoilers);
    }

    /// <summary>
    /// Deletes the calling user's review for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>No content.</returns>
    [HttpDelete("Items/{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteReview([FromRoute, Required] Guid itemId)
        => _reviewsManager.DeleteReview(itemId, User.GetUserId()) ? NoContent() : NotFound();
}
