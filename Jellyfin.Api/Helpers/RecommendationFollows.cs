using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Api.Helpers;

/// <summary>
/// Stores recommendation exclusions independently of playback progress.
/// </summary>
public static class RecommendationFollows
{
    private const string Client = "recommendation-follows";

    /// <summary>Gets the recommendation identity shared by episodes of a series.</summary>
    /// <param name="item">The media item.</param>
    /// <returns>The series or item identifier.</returns>
    public static Guid Identity(BaseItem item)
        => item is Episode episode && !episode.SeriesId.Equals(Guid.Empty) ? episode.SeriesId : item.Id;

    /// <summary>Gets the current user's excluded recommendation identities.</summary>
    /// <param name="preferences">The preferences manager.</param>
    /// <param name="userId">The user identifier.</param>
    /// <returns>Excluded identifiers.</returns>
    public static HashSet<Guid> GetExcluded(IDisplayPreferencesManager preferences, Guid userId)
        => preferences.ListCustomItemDisplayPreferences(userId, Guid.Empty, Client)
            .Keys.Where(k => Guid.TryParse(k, out _)).Select(Guid.Parse).ToHashSet();

    /// <summary>Changes the current user's exclusion for an item.</summary>
    /// <param name="preferences">The preferences manager.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="item">The item.</param>
    /// <param name="excluded">Whether to exclude it.</param>
    public static void SetExcluded(IDisplayPreferencesManager preferences, Guid userId, BaseItem item, bool excluded)
    {
        var values = preferences.ListCustomItemDisplayPreferences(userId, Guid.Empty, Client);
        var key = Identity(item).ToString("N");
        if (excluded)
        {
            values[key] = "1";
        }
        else if (!values.Remove(key))
        {
            return;
        }

        preferences.SetCustomItemDisplayPreferences(userId, Guid.Empty, Client, values);
    }
}
