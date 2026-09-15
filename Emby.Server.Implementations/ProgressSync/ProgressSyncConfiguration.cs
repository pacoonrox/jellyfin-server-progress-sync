using System.Collections.Generic;

namespace Emby.Server.Implementations.ProgressSync;

internal sealed class ProgressSyncConfiguration
{
    public List<ProgressSyncSeriesConfiguration> Series { get; set; } = [];
}
