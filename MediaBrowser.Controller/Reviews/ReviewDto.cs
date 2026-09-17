using System;

namespace MediaBrowser.Controller.Reviews;

/// <summary>
/// A single user's rating and/or comment on a media item.
/// </summary>
public class ReviewDto
{
    /// <summary>
    /// Gets or sets the item id the review applies to.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who wrote the review.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the user who wrote the review.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the rating from 1-10, or null for a comment-only review. Precision is
    /// truncated to 2 decimal places.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    /// Gets or sets the free-text comment, or null for a rating-only review.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the comment contains spoilers.
    /// </summary>
    public bool ContainsSpoilers { get; set; }

    /// <summary>
    /// Gets or sets when the review was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the review was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
