using System;
using System.Collections.Generic;

namespace MediaBrowser.Controller.ProgressSync;

/// <summary>
/// Manages show progress sync groups.
/// </summary>
public interface IProgressSyncManager
{
    /// <summary>
    /// Gets the configured users for a series.
    /// </summary>
    IReadOnlyList<Guid> GetSeriesUsers(Guid seriesId);

    /// <summary>
    /// Adds a user to a series progress sync group.
    /// </summary>
    ProgressSyncSeriesDto AddUser(Guid seriesId, Guid sourceUserId, Guid targetUserId);

    /// <summary>
    /// Removes a user from a series progress sync group.
    /// </summary>
    ProgressSyncSeriesDto RemoveUser(Guid seriesId, Guid targetUserId);
}
