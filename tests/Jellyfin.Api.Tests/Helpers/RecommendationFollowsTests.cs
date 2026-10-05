using System;
using System.Collections.Generic;
using Jellyfin.Api.Helpers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Helpers;

public class RecommendationFollowsTests
{
    [Fact]
    public void UnfollowEpisode_HidesSeriesOnlyForThatUser_AndFollowRestoresIt()
    {
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var firstEpisode = new Episode { Id = Guid.NewGuid(), SeriesId = seriesId };
        var laterEpisode = new Episode { Id = Guid.NewGuid(), SeriesId = seriesId };
        var unrelatedMovie = new Movie { Id = Guid.NewGuid() };
        var saved = new Dictionary<Guid, Dictionary<string, string?>>();
        var preferences = new Mock<IDisplayPreferencesManager>();
        preferences.Setup(p => p.ListCustomItemDisplayPreferences(It.IsAny<Guid>(), Guid.Empty, "recommendation-follows"))
            .Returns((Guid userId, Guid _, string _) => saved.TryGetValue(userId, out var values)
                ? new Dictionary<string, string?>(values)
                : new Dictionary<string, string?>());
        preferences.Setup(p => p.SetCustomItemDisplayPreferences(It.IsAny<Guid>(), Guid.Empty, "recommendation-follows", It.IsAny<Dictionary<string, string?>>()))
            .Callback((Guid userId, Guid _, string _, Dictionary<string, string?> values) => saved[userId] = new Dictionary<string, string?>(values));

        RecommendationFollows.SetExcluded(preferences.Object, firstUser, firstEpisode, true);
        Assert.Contains(RecommendationFollows.Identity(laterEpisode), RecommendationFollows.GetExcluded(preferences.Object, firstUser));
        Assert.DoesNotContain(RecommendationFollows.Identity(laterEpisode), RecommendationFollows.GetExcluded(preferences.Object, secondUser));
        Assert.DoesNotContain(RecommendationFollows.Identity(unrelatedMovie), RecommendationFollows.GetExcluded(preferences.Object, firstUser));

        RecommendationFollows.SetExcluded(preferences.Object, firstUser, laterEpisode, false);
        Assert.Empty(RecommendationFollows.GetExcluded(preferences.Object, firstUser));
    }
}
