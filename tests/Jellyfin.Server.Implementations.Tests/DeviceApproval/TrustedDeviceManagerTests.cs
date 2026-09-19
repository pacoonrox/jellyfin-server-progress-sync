using System;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.DeviceApproval;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Entities.Security;
using Jellyfin.Database.Implementations.Locking;
using Jellyfin.Database.Providers.Sqlite;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.DeviceApproval;

public sealed class TrustedDeviceManagerTests : IDisposable
{
    private const string Credential = "0123456789012345678901234567890123456789012";
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<JellyfinDbContext> _options;
    private readonly TrustedDeviceManager _subject;

    public TrustedDeviceManagerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<JellyfinDbContext>().UseSqlite(_connection).Options;
        using var context = CreateContext();
        context.Database.EnsureCreated();
        var factory = new Mock<IDbContextFactory<JellyfinDbContext>>();
        factory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateContext);
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(x => x.Configuration).Returns(new ServerConfiguration { TrustedDevicesEnabled = true, TrustedDeviceDefaultDays = 30 });
        _subject = new TrustedDeviceManager(factory.Object, configuration.Object, new Mock<IUserManager>().Object);
    }

    [Fact]
    public async Task Trust_IsIsolatedByUserAndCredential()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        await _subject.IssueAsync(alice, Credential, "installation", "Direct", alice, false);
        await _subject.IssueAsync(bob, Credential, "installation", "Direct", bob, false);

        Assert.True(await Validate(alice, Credential));
        Assert.True(await Validate(bob, Credential));
        Assert.False(await Validate(alice, new string('z', 43)));
        Assert.False(await Validate(Guid.NewGuid(), Credential));
    }

    [Fact]
    public async Task UseDoesNotRenewAndRevocationIsImmediate()
    {
        var user = Guid.NewGuid();
        await _subject.IssueAsync(user, Credential, "installation", "Direct", user, false);
        var before = (await _subject.QueryAsync(null, user, null))[0];
        Assert.True(await Validate(user, Credential));
        var after = (await _subject.QueryAsync(null, user, null))[0];
        Assert.Equal(before.ExpiresUtc, after.ExpiresUtc);

        await _subject.RevokeAsync(after.Id, user);
        Assert.False(await Validate(user, Credential));
    }

    [Fact]
    public async Task NeverTrust_BlocksAutomaticReissueUntilAdministratorExplicitlyTrustsAgain()
    {
        var user = Guid.NewGuid();
        await _subject.IssueAsync(user, Credential, "installation", "Direct", user, false);
        var record = (await _subject.QueryAsync(null, user, null))[0];

        await _subject.NeverTrustAsync(record.Id, user);
        await _subject.IssueAsync(user, Credential, "installation", "Direct", user, false);
        await _subject.IssueAsync(user, new string('z', 43), "installation", "Direct", user, false);

        var blocked = (await _subject.QueryAsync(null, user, null))[0];
        Assert.Equal("Never", blocked.State);
        Assert.False(await Validate(user, Credential));

        await _subject.TrustObservedAsync(blocked.Id, null, DateTime.UtcNow.AddDays(30), user);
        Assert.True(await Validate(user, Credential));
    }

    [Fact]
    public async Task AutomaticLogoutRequiresFreshTwoFactorWithoutDeletingAdministratorTrust()
    {
        var user = Guid.NewGuid();
        await _subject.IssueAsync(user, Credential, "installation", "Administrator", user, true);

        await _subject.RequireFreshTwoFactorAsync(user, "installation");
        Assert.False(await Validate(user, Credential));

        await _subject.ObserveAsync(user, Credential, "installation", "Web", "2.0", "Browser", "Web", "OS", "192.0.2.1", directTwoFactorVerified: true);
        Assert.True(await Validate(user, Credential));
    }

    [Theory]
    [InlineData("Seerr")]
    [InlineData("Jellyseerr")]
    [InlineData("Overseerr")]
    public async Task SeerrClientsAreNotObserved(string appName)
    {
        var user = Guid.NewGuid();

        await _subject.ObserveAsync(user, null, "installation", appName, "2.0", "Service", string.Empty, string.Empty, "192.0.2.1");

        Assert.Empty(await _subject.QueryAsync(null, user, null));
    }

    [Theory]
    [InlineData("Legacy", true)]
    [InlineData("TrustedDevice", true)]
    [InlineData("Portal", true)]
    public async Task ApprovalEligibility_AllowsEveryAuthenticatedSession(string provenance, bool expected)
    {
        var token = await AddSession(provenance);

        Assert.Equal(expected, await _subject.CanApproveAsync(token));
    }

    public void Dispose() => _connection.Dispose();

    private Task<bool> Validate(Guid user, string credential)
        => _subject.ValidateAsync(user, credential, "installation", "Web", "2.0", "Browser", "192.0.2.1");

    private async Task<string> AddSession(string provenance)
    {
        await using var context = CreateContext();
        var user = new User($"user-{Guid.NewGuid():N}", "default", "default");
        var device = new Device(user.Id, "Web", "2.0", "Browser", $"device-{Guid.NewGuid():N}")
        {
            AuthenticationProvenance = provenance
        };
        context.Users.Add(user);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.AccessToken;
    }

    private JellyfinDbContext CreateContext()
        => new(_options, NullLogger<JellyfinDbContext>.Instance, new SqliteDatabaseProvider(null!, NullLogger<SqliteDatabaseProvider>.Instance), new NoLockBehavior(NullLogger<NoLockBehavior>.Instance));
}
