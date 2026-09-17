using System.Collections.Generic;

namespace Emby.Server.Implementations.Reviews;

internal sealed class ReviewsConfiguration
{
    public List<ReviewEntry> Reviews { get; set; } = [];
}
