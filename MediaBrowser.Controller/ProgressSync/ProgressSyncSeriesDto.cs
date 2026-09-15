using System;
using System.Collections.Generic;

namespace MediaBrowser.Controller.ProgressSync;

/// <summary>
/// Progress sync configuration for a series.
/// </summary>
public class ProgressSyncSeriesDto
{
    /// <summary>
    /// Gets or sets the series id.
    /// </summary>
    public Guid SeriesId { get; set; }

    /// <summary>
    /// Gets or sets the synced user ids.
    /// </summary>
    public IReadOnlyList<Guid> UserIds { get; set; } = [];
}
