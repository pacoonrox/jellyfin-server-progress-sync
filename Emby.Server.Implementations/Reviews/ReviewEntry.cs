using System;

namespace Emby.Server.Implementations.Reviews;

internal sealed class ReviewEntry
{
    public Guid ItemId { get; set; }

    public Guid UserId { get; set; }

    public string? UserName { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public bool ContainsSpoilers { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
