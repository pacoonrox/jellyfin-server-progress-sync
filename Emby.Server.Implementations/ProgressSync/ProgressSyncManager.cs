using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.ProgressSync;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.ProgressSync;

/// <inheritdoc />
public sealed class ProgressSyncManager : IProgressSyncManager
{
    private readonly object _syncLock = new();
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ProgressSyncManager> _logger;
    private readonly string _path;
    private ProgressSyncConfiguration? _configuration;
    private int _isPropagating;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressSyncManager"/> class.
    /// </summary>
    public ProgressSyncManager(
        IServerConfigurationManager configurationManager,
        IUserManager userManager,
        IUserDataManager userDataManager,
        ILibraryManager libraryManager,
        ILogger<ProgressSyncManager> logger)
    {
        _userManager = userManager;
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
        _logger = logger;
        _path = Path.Combine(configurationManager.ApplicationPaths.ConfigurationDirectoryPath, "progresssync.json");
    }

    /// <inheritdoc />
    public IReadOnlyList<Guid> GetSeriesUsers(Guid seriesId)
    {
        lock (_syncLock)
        {
            return GetSeriesConfiguration(seriesId)?.UserIds.ToArray() ?? [];
        }
    }

    /// <inheritdoc />
    public ProgressSyncSeriesDto AddUser(Guid seriesId, Guid sourceUserId, Guid targetUserId)
    {
        EnsureUserExists(sourceUserId);
        EnsureUserExists(targetUserId);

        lock (_syncLock)
        {
            var series = GetOrCreateSeriesConfiguration(seriesId);
            AddDistinct(series.UserIds, sourceUserId);
            AddDistinct(series.UserIds, targetUserId);
            SaveConfiguration();
        }

        SynchronizeExistingSeriesProgress(seriesId, sourceUserId);
        return new ProgressSyncSeriesDto { SeriesId = seriesId, UserIds = GetSeriesUsers(seriesId) };
    }

    /// <inheritdoc />
    public ProgressSyncSeriesDto RemoveUser(Guid seriesId, Guid targetUserId)
    {
        lock (_syncLock)
        {
            var series = GetSeriesConfiguration(seriesId);
            if (series is not null)
            {
                series.UserIds.RemoveAll(i => i.Equals(targetUserId));
                if (series.UserIds.Count < 2)
                {
                    Configuration.Series.Remove(series);
                }

                SaveConfiguration();
            }
        }

        return new ProgressSyncSeriesDto { SeriesId = seriesId, UserIds = GetSeriesUsers(seriesId) };
    }

    /// <summary>
    /// Propagates a saved user data change to other synced users.
    /// </summary>
    public void HandleUserDataSaved(UserDataSaveEventArgs e)
    {
        if (_isPropagating > 0 || e.Item is not Episode episode || episode.SeriesId.Equals(Guid.Empty))
        {
            return;
        }

        var userIds = GetSeriesUsers(episode.SeriesId);
        if (userIds.Count < 2 || !userIds.Contains(e.UserId))
        {
            return;
        }

        var sourceUser = _userManager.GetUserById(e.UserId);
        if (sourceUser is null)
        {
            return;
        }

        var sourceUserData = _userDataManager.GetUserData(sourceUser, episode);
        if (sourceUserData is null)
        {
            return;
        }

        try
        {
            _isPropagating++;
            foreach (var targetUserId in userIds.Where(i => !i.Equals(e.UserId)))
            {
                var targetUser = _userManager.GetUserById(targetUserId);
                if (targetUser is null)
                {
                    continue;
                }

                CopyUserData(targetUser, episode, sourceUserData, e.SaveReason);
            }
        }
        finally
        {
            _isPropagating--;
        }
    }

    private void SynchronizeExistingSeriesProgress(Guid seriesId, Guid sourceUserId)
    {
        var sourceUser = _userManager.GetUserById(sourceUserId);
        if (sourceUser is null)
        {
            return;
        }

        var series = _libraryManager.GetItemById<Series>(seriesId, sourceUser);
        if (series is null)
        {
            return;
        }

        var userIds = GetSeriesUsers(seriesId);
        var episodes = series.GetEpisodes(sourceUser, new DtoOptions(false), false).OfType<Episode>().ToArray();

        try
        {
            _isPropagating++;
            foreach (var episode in episodes)
            {
                var winningUserData = userIds
                    .Select(userId => _userManager.GetUserById(userId))
                    .Where(user => user is not null)
                    .Select(user => _userDataManager.GetUserData(user!, episode))
                    .Where(userData => userData is not null)
                    .OrderByDescending(userData => userData!.Played)
                    .ThenByDescending(userData => userData!.PlaybackPositionTicks)
                    .ThenByDescending(userData => userData!.LastPlayedDate ?? DateTime.MinValue)
                    .FirstOrDefault();

                if (winningUserData is null)
                {
                    continue;
                }

                foreach (var targetUserId in userIds)
                {
                    var targetUser = _userManager.GetUserById(targetUserId);
                    if (targetUser is not null)
                    {
                        CopyUserData(targetUser, episode, winningUserData, UserDataSaveReason.UpdateUserData);
                    }
                }
            }
        }
        finally
        {
            _isPropagating--;
        }
    }

    private void CopyUserData(User targetUser, BaseItem item, UserItemData source, UserDataSaveReason reason)
    {
        var copy = _userDataManager.GetUserData(targetUser, item) ?? new UserItemData { Key = source.Key };
        copy.LastPlayedDate = source.LastPlayedDate;
        copy.PlaybackPositionTicks = source.PlaybackPositionTicks;
        copy.PlayCount = source.PlayCount;
        copy.Played = source.Played;

        _userDataManager.SaveUserData(targetUser, item, copy, reason, default);
    }

    private ProgressSyncConfiguration Configuration
    {
        get
        {
            lock (_syncLock)
            {
                return _configuration ??= LoadConfiguration();
            }
        }
    }

    private ProgressSyncConfiguration LoadConfiguration()
    {
        if (!File.Exists(_path))
        {
            return new ProgressSyncConfiguration();
        }

        try
        {
            return JsonSerializer.Deserialize<ProgressSyncConfiguration>(File.ReadAllText(_path)) ?? new ProgressSyncConfiguration();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Unable to read progress sync configuration from {Path}", _path);
            return new ProgressSyncConfiguration();
        }
    }

    private void SaveConfiguration()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(Configuration, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }

    private ProgressSyncSeriesConfiguration? GetSeriesConfiguration(Guid seriesId)
        => Configuration.Series.FirstOrDefault(i => i.SeriesId.Equals(seriesId));

    private ProgressSyncSeriesConfiguration GetOrCreateSeriesConfiguration(Guid seriesId)
    {
        var series = GetSeriesConfiguration(seriesId);
        if (series is not null)
        {
            return series;
        }

        series = new ProgressSyncSeriesConfiguration { SeriesId = seriesId };
        Configuration.Series.Add(series);
        return series;
    }

    private void EnsureUserExists(Guid userId)
    {
        if (_userManager.GetUserById(userId) is null)
        {
            throw new ArgumentException("User not found.", nameof(userId));
        }
    }

    private static void AddDistinct(List<Guid> values, Guid value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
        }
    }
}
