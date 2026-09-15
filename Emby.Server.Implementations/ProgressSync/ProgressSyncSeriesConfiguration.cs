using System;
using System.Collections.Generic;

namespace Emby.Server.Implementations.ProgressSync;

internal sealed class ProgressSyncSeriesConfiguration
{
    public Guid SeriesId { get; set; }

    public List<Guid> UserIds { get; set; } = [];
}
