using System;

namespace MediaBrowser.Controller.Reviews;

/// <summary>
/// Aggregate rating information for a single media item.
/// </summary>
public class ItemRatingSummaryDto
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the average rating across all reviews with a rating, or null if none.
    /// </summary>
    public double? AverageRating { get; set; }

    /// <summary>
    /// Gets or sets the number of reviews that include a rating.
    /// </summary>
    public int RatingCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of reviews (including comment-only reviews).
    /// </summary>
    public int ReviewCount { get; set; }
}
