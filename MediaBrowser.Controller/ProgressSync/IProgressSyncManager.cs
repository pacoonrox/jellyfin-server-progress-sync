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
    /// <param name="seriesId">The series identifier.</param>
    /// <returns>The users configured for the series.</returns>
    IReadOnlyList<Guid> GetSeriesUsers(Guid seriesId);

    /// <summary>
    /// Adds a user to a series progress sync group.
    /// </summary>
    /// <param name="seriesId">The series identifier.</param>
    /// <param name="sourceUserId">The source user identifier.</param>
    /// <param name="targetUserId">The user to add.</param>
    /// <returns>The updated sync group.</returns>
    ProgressSyncSeriesDto AddUser(Guid seriesId, Guid sourceUserId, Guid targetUserId);

    /// <summary>
    /// Removes a user from a series progress sync group.
    /// </summary>
    /// <param name="seriesId">The series identifier.</param>
    /// <param name="targetUserId">The user to remove.</param>
    /// <returns>The updated sync group.</returns>
    ProgressSyncSeriesDto RemoveUser(Guid seriesId, Guid targetUserId);
}
