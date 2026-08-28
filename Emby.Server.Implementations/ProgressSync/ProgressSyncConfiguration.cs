using System;
using System.Collections.Generic;

namespace Emby.Server.Implementations.ProgressSync;

internal sealed class ProgressSyncConfiguration
{
    public List<ProgressSyncSeriesConfiguration> Series { get; set; } = [];
}

internal sealed class ProgressSyncSeriesConfiguration
{
    public Guid SeriesId { get; set; }

    public List<Guid> UserIds { get; set; } = [];
}
