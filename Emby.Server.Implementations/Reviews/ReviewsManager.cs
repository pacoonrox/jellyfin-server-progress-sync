using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Reviews;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Reviews;

/// <inheritdoc />
public sealed class ReviewsManager : IReviewsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _syncLock = new();
    private readonly ILogger<ReviewsManager> _logger;
    private readonly string _path;
    private ReviewsConfiguration? _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewsManager"/> class.
    /// </summary>
    /// <param name="configurationManager">The server configuration manager.</param>
    /// <param name="logger">The logger.</param>
    public ReviewsManager(IServerConfigurationManager configurationManager, ILogger<ReviewsManager> logger)
    {
        _logger = logger;
        _path = Path.Combine(configurationManager.ApplicationPaths.ConfigurationDirectoryPath, "reviews.json");
    }

    private ReviewsConfiguration Configuration
    {
        get
        {
            lock (_syncLock)
            {
                return _configuration ??= LoadConfiguration();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ReviewDto> GetReviews(Guid itemId)
    {
        lock (_syncLock)
        {
            return Configuration.Reviews
                .Where(r => r.ItemId.Equals(itemId))
                .OrderByDescending(r => r.CreatedAt)
                .Select(ToDto)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public ReviewDto? GetReview(Guid itemId, Guid userId)
    {
        lock (_syncLock)
        {
            var entry = FindEntry(itemId, userId);
            return entry is null ? null : ToDto(entry);
        }
    }

    /// <inheritdoc />
    public ItemRatingSummaryDto GetSummary(Guid itemId)
    {
        lock (_syncLock)
        {
            return BuildSummary(itemId, Configuration.Reviews.Where(r => r.ItemId.Equals(itemId)));
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Guid, ItemRatingSummaryDto> GetSummaries(IReadOnlyList<Guid> itemIds)
    {
        var idSet = new HashSet<Guid>(itemIds);
        lock (_syncLock)
        {
            return Configuration.Reviews
                .Where(r => idSet.Contains(r.ItemId))
                .GroupBy(r => r.ItemId)
                .ToDictionary(g => g.Key, g => BuildSummary(g.Key, g));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ReviewDto> GetReviewsByUser(Guid userId)
    {
        lock (_syncLock)
        {
            return Configuration.Reviews
                .Where(r => r.UserId.Equals(userId))
                .OrderByDescending(r => r.CreatedAt)
                .Select(ToDto)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ItemRatingSummaryDto> GetTopRated(int minRatingCount, int limit)
    {
        lock (_syncLock)
        {
            return Configuration.Reviews
                .GroupBy(r => r.ItemId)
                .Select(g => BuildSummary(g.Key, g))
                .Where(s => s.RatingCount >= minRatingCount && s.AverageRating.HasValue)
                .OrderByDescending(s => s.AverageRating)
                .ThenByDescending(s => s.RatingCount)
                .Take(limit)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public ReviewDto UpsertReview(Guid itemId, Guid userId, string? userName, int? rating, string? comment, bool containsSpoilers)
    {
        if (rating is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 10.");
        }

        lock (_syncLock)
        {
            var now = DateTime.UtcNow;
            var entry = FindEntry(itemId, userId);
            if (entry is null)
            {
                entry = new ReviewEntry { ItemId = itemId, UserId = userId, CreatedAt = now };
                Configuration.Reviews.Add(entry);
            }

            entry.UserName = userName;
            entry.Rating = rating;
            entry.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            entry.ContainsSpoilers = containsSpoilers;
            entry.UpdatedAt = now;

            SaveConfiguration();
            return ToDto(entry);
        }
    }

    /// <inheritdoc />
    public bool DeleteReview(Guid itemId, Guid userId)
    {
        lock (_syncLock)
        {
            var removed = Configuration.Reviews.RemoveAll(r => r.ItemId.Equals(itemId) && r.UserId.Equals(userId));
            if (removed > 0)
            {
                SaveConfiguration();
            }

            return removed > 0;
        }
    }

    private static ItemRatingSummaryDto BuildSummary(Guid itemId, IEnumerable<ReviewEntry> reviews)
    {
        var materialized = reviews as ReviewEntry[] ?? reviews.ToArray();
        var rated = materialized.Where(r => r.Rating.HasValue).ToArray();
        return new ItemRatingSummaryDto
        {
            ItemId = itemId,
            RatingCount = rated.Length,
            ReviewCount = materialized.Length,
            AverageRating = rated.Length == 0 ? null : rated.Average(r => r.Rating!.Value)
        };
    }

    private ReviewEntry? FindEntry(Guid itemId, Guid userId)
        => Configuration.Reviews.FirstOrDefault(r => r.ItemId.Equals(itemId) && r.UserId.Equals(userId));

    private static ReviewDto ToDto(ReviewEntry entry) => new()
    {
        ItemId = entry.ItemId,
        UserId = entry.UserId,
        UserName = entry.UserName,
        Rating = entry.Rating,
        Comment = entry.Comment,
        ContainsSpoilers = entry.ContainsSpoilers,
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt
    };

    private ReviewsConfiguration LoadConfiguration()
    {
        if (!File.Exists(_path))
        {
            return new ReviewsConfiguration();
        }

        try
        {
            return JsonSerializer.Deserialize<ReviewsConfiguration>(File.ReadAllText(_path)) ?? new ReviewsConfiguration();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Unable to read reviews configuration from {Path}", _path);
            return new ReviewsConfiguration();
        }
    }

    private void SaveConfiguration()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(Configuration, JsonOptions);
        File.WriteAllText(_path, json);
    }
}
