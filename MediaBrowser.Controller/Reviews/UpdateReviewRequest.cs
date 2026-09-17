namespace MediaBrowser.Controller.Reviews;

/// <summary>
/// Request body for creating or updating a review.
/// </summary>
public class UpdateReviewRequest
{
    /// <summary>
    /// Gets or sets the rating from 1-10, or null to leave it unrated. Decimal values are
    /// truncated to 2 decimal places.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    /// Gets or sets the comment text, or null/empty for no comment.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the comment contains spoilers.
    /// </summary>
    public bool ContainsSpoilers { get; set; }
}
