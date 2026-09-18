using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.DeviceApproval;
using Jellyfin.Data;
using Jellyfin.Data.Queries;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Entities.Security;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Database.Implementations.Locking;
using Jellyfin.Database.Providers.Sqlite;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.DeviceApproval;
using MediaBrowser.Model.Querying;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.DeviceApproval;

/// <summary>
/// Exercises the <see cref="IdleLogoutDeviceOverride"/> EF Core model (entity, configuration, and
/// <see cref="JellyfinDbContext"/> wiring) against a real SQLite schema built via <c>EnsureCreated</c>,
/// and the <see cref="TrustedDeviceManager"/> read/write round trip built on top of it.
/// </summary>
public sealed class IdleLogoutPolicyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<JellyfinDbContext> _options;
    private readonly Mock<IUserManager> _users = new();
    private readonly Mock<IDeviceManager> _devices = new();
    private readonly TrustedDeviceManager _subject;

    public IdleLogoutPolicyTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<JellyfinDbContext>().UseSqlite(_connection).Options;
        using (var context = CreateContext())
        {
            context.Database.EnsureCreated();
        }

        _devices.Setup(m => m.GetDevices(It.IsAny<DeviceQuery>())).Returns(new QueryResult<Device>(new List<Device>()));

        var factory = new Mock<IDbContextFactory<JellyfinDbContext>>();
        factory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateContext);
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(x => x.Configuration).Returns(new ServerConfiguration());
        _subject = new TrustedDeviceManager(factory.Object, configuration.Object, _users.Object, _devices.Object);
    }

    [Fact]
    public async Task SetIdleLogoutPolicyAsync_PersistsScalarFieldsAndDeviceOverrides_AcrossSeparateContexts()
    {
        var user = new User($"user-{Guid.NewGuid():N}", "default", "default");
        await using (var context = CreateContext())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var policy = new IdleLogoutPolicyDto
        {
            Enabled = true,
            Minutes = 42,
            ScopeMode = InactiveLogoutScope.SelectedManual,
            SelectedDeviceIds = new[] { "unused-in-manual-mode" },
            ManualFutureDefaultSubject = false,
            DeviceOverrides = new Dictionary<string, bool>
            {
                ["device-a"] = true,
                ["device-b"] = false
            }
        };

        await _subject.SetIdleLogoutPolicyAsync(user.Id, policy, Guid.NewGuid());

        // Re-load through a fresh context/tracked User (as UserManager.GetUserById would), never
        // reusing the entity instance SetIdleLogoutPolicyAsync mutated, to prove it round-trips
        // through SQLite rather than surviving only in memory.
        await using var verify = CreateContext();
        var reloaded = await verify.Users
            .Include(u => u.Permissions)
            .Include(u => u.Preferences)
            .Include(u => u.IdleLogoutDeviceOverrides)
            .FirstAsync(u => u.Id.Equals(user.Id), TestContext.Current.CancellationToken);

        Assert.True(reloaded.IsIdleLogoutEnabled());
        Assert.Equal(42, reloaded.GetIdleLogoutMinutes());
        Assert.Equal(InactiveLogoutScope.SelectedManual, reloaded.GetIdleLogoutScopeMode());
        Assert.False(reloaded.HasPermission(PermissionKind.IdleLogoutManualFutureDefault));
        Assert.True(reloaded.IsDeviceSubjectToIdleLogout("device-a"));
        Assert.False(reloaded.IsDeviceSubjectToIdleLogout("device-b"));
        // No override recorded and future-default is false: falls through to the default.
        Assert.False(reloaded.IsDeviceSubjectToIdleLogout("device-c"));
    }

    [Fact]
    public async Task SetIdleLogoutPolicyAsync_ReplacingOverrides_RemovesStaleRowsRatherThanAccumulating()
    {
        var user = new User($"user-{Guid.NewGuid():N}", "default", "default");
        await using (var context = CreateContext())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var first = new IdleLogoutPolicyDto
        {
            Enabled = true,
            Minutes = 5,
            ScopeMode = InactiveLogoutScope.SelectedManual,
            DeviceOverrides = new Dictionary<string, bool> { ["device-a"] = true, ["device-b"] = true }
        };
        await _subject.SetIdleLogoutPolicyAsync(user.Id, first, Guid.NewGuid());

        var second = new IdleLogoutPolicyDto
        {
            Enabled = true,
            Minutes = 5,
            ScopeMode = InactiveLogoutScope.SelectedManual,
            DeviceOverrides = new Dictionary<string, bool> { ["device-a"] = false }
        };
        await _subject.SetIdleLogoutPolicyAsync(user.Id, second, Guid.NewGuid());

        await using var verify = CreateContext();
        var overrides = await verify.IdleLogoutDeviceOverrides.Where(o => o.UserId.Equals(user.Id)).ToListAsync(TestContext.Current.CancellationToken);

        var overrideRow = Assert.Single(overrides);
        Assert.Equal("device-a", overrideRow.DeviceId);
        Assert.False(overrideRow.Subject);
    }

    [Fact]
    public async Task GetIdleLogoutPolicyAsync_ReflectsUserState_WithoutRequiringADatabaseRoundTrip()
    {
        var user = new User($"user-{Guid.NewGuid():N}", "default", "default");
        user.SetPermission(PermissionKind.IdleLogoutEnabled, true);
        user.SetIdleLogoutMinutes(7);
        user.SetIdleLogoutScopeMode(InactiveLogoutScope.AllDevicesExceptSelected);
        user.SetPreference(PreferenceKind.IdleLogoutSelectedDeviceIds, new[] { "device-x" });
        _users.Setup(m => m.GetUserById(user.Id)).Returns(user);

        var policy = await _subject.GetIdleLogoutPolicyAsync(user.Id);

        Assert.True(policy.Enabled);
        Assert.Equal(7, policy.Minutes);
        Assert.Equal(InactiveLogoutScope.AllDevicesExceptSelected, policy.ScopeMode);
        Assert.Contains("device-x", policy.SelectedDeviceIds);
    }

    public void Dispose() => _connection.Dispose();

    private JellyfinDbContext CreateContext()
        => new(_options, NullLogger<JellyfinDbContext>.Instance, new SqliteDatabaseProvider(null!, NullLogger<SqliteDatabaseProvider>.Instance), new NoLockBehavior(NullLogger<NoLockBehavior>.Instance));
}
