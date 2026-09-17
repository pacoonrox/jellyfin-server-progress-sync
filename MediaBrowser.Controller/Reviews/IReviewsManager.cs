using System;
using System.Collections.Generic;

namespace MediaBrowser.Controller.Reviews;

/// <summary>
/// Manages user ratings and comments on media items (movies, series, seasons, or episodes).
/// </summary>
public interface IReviewsManager
{
    /// <summary>
    /// Gets every review for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The reviews, newest first.</returns>
    IReadOnlyList<ReviewDto> GetReviews(Guid itemId);

    /// <summary>
    /// Gets a single user's review for an item, if any.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>The review, or null if the user has not reviewed the item.</returns>
    ReviewDto? GetReview(Guid itemId, Guid userId);

    /// <summary>
    /// Gets the aggregate rating summary for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The rating summary.</returns>
    ItemRatingSummaryDto GetSummary(Guid itemId);

    /// <summary>
    /// Gets the aggregate rating summaries for a set of items.
    /// </summary>
    /// <param name="itemIds">The item ids.</param>
    /// <returns>A summary per item, keyed by item id. Items with no reviews are omitted.</returns>
    IReadOnlyDictionary<Guid, ItemRatingSummaryDto> GetSummaries(IReadOnlyList<Guid> itemIds);

    /// <summary>
    /// Gets every review created by a user, across all items.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The user's reviews, newest first.</returns>
    IReadOnlyList<ReviewDto> GetReviewsByUser(Guid userId);

    /// <summary>
    /// Gets the item ids with the highest average rating.
    /// </summary>
    /// <param name="minRatingCount">The minimum number of ratings an item must have to be included.</param>
    /// <param name="limit">The maximum number of results.</param>
    /// <returns>Rating summaries, sorted by average rating descending.</returns>
    IReadOnlyList<ItemRatingSummaryDto> GetTopRated(int minRatingCount, int limit);

    /// <summary>
    /// Creates or updates the calling user's review for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="userName">The user name, stored for display.</param>
    /// <param name="rating">The rating from 1-10, or null to leave/keep it unset.</param>
    /// <param name="comment">The comment text, or null to leave/keep it unset.</param>
    /// <param name="containsSpoilers">Whether the comment contains spoilers.</param>
    /// <returns>The saved review.</returns>
    ReviewDto UpsertReview(Guid itemId, Guid userId, string? userName, int? rating, string? comment, bool containsSpoilers);

    /// <summary>
    /// Deletes a user's review for an item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>True if a review was deleted.</returns>
    bool DeleteReview(Guid itemId, Guid userId);
}
