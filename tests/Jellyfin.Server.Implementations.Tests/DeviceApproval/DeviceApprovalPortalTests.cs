using System;
using System.Linq;
using System.Threading.Tasks;
using Emby.Server.Implementations.DeviceApproval;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.DeviceApproval;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.DeviceApproval;
using Moq;
using Xunit;

#pragma warning disable SA1501, SA1107

namespace Jellyfin.Server.Implementations.Tests.DeviceApproval;

public sealed class DeviceApprovalPortalTests
{
    private readonly Mock<ITrustedDeviceManager> _trust = new();
    private readonly DeviceApprovalPortal _subject;
    private readonly MutableTimeProvider _time = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly AuthorizationInfo _client = new() { Device = "Living Room", DeviceId = "installation-a", Client = "Jellyfin Web", Version = "1.2.3" };

    public DeviceApprovalPortalTests()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(x => x.Configuration).Returns(new ServerConfiguration { TrustedDevicesEnabled = true });
        _trust.Setup(x => x.CanApproveAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(true);
        _trust.Setup(x => x.AuditAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _subject = new DeviceApprovalPortal(configuration.Object, _trust.Object, new Mock<ISessionManager>().Object, new Mock<IUserManager>().Object, _time);
    }

    [Fact]
    public async Task Queue_RedactsIpForRegularUsers()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");

        Assert.Null(Assert.Single(_subject.GetQueue(false)).RequestingIpAddress);
        Assert.Equal("192.0.2.10", Assert.Single(_subject.GetQueue(true)).RequestingIpAddress);
        Assert.Equal("jellyfin.example.test", Assert.Single(_subject.GetQueue(false)).ConnectionDomain);
    }

    [Fact]
    public async Task ConcurrentSelection_HasExactlyOneWinner()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        var id = Assert.Single(_subject.GetQueue(false)).Id;

        var attempts = new[]
        {
            TrySelect(id, Guid.NewGuid(), "direct-1"),
            TrySelect(id, Guid.NewGuid(), "direct-2")
        };

        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(x => x));
    }

    [Fact]
    public async Task PortalProvenance_CannotSelectAnotherDevice()
    {
        _trust.Setup(x => x.CanApproveAsync("portal-token", false)).ReturnsAsync(false);
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        var id = Assert.Single(_subject.GetQueue(false)).Id;

        await Assert.ThrowsAsync<AuthenticationException>(() => _subject.SelectAsync(id, Guid.NewGuid(), "portal-token", false));
    }

    [Fact]
    public async Task Selection_NoReturnsToQueue()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        var actor = Guid.NewGuid();
        var id = Assert.Single(_subject.GetQueue(false)).Id;
        var selected = await _subject.SelectAsync(id, actor, "direct", false);

        Assert.Equal(DeviceApprovalState.Selected, selected.State);
        Assert.Null(await _subject.ConfirmAsync(id, actor, "direct", false, false, false));
        Assert.Equal(DeviceApprovalState.Pending, Assert.Single(_subject.GetQueue(false)).State);
    }

    [Fact]
    public async Task ApiKeyFlag_IsForwardedToTrustProvenanceCheck()
    {
        _trust.Setup(x => x.CanApproveAsync("api-key-token", true)).ReturnsAsync(true);
        _trust.Setup(x => x.CanApproveAsync("api-key-token", false)).ReturnsAsync(false);
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        var id = Assert.Single(_subject.GetQueue(false)).Id;

        var selected = await _subject.SelectAsync(id, Guid.NewGuid(), "api-key-token", true);
        Assert.Equal(DeviceApprovalState.Selected, selected.State);
    }

    [Fact]
    public async Task Confirmation_IssuesTrustOnlyWhenSelected()
    {
        var user = new User("approval-user", "default", "default");
        var users = new Mock<IUserManager>();
        users.Setup(x => x.GetUserById(user.Id)).Returns(user);
        var sessions = new Mock<ISessionManager>();
        sessions.Setup(x => x.AuthenticatePortalSession(It.IsAny<AuthenticationRequest>())).ReturnsAsync(new AuthenticationResult { AccessToken = "token" });
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(x => x.Configuration).Returns(new ServerConfiguration { DeviceApprovalAvailable = true, TrustedDevicesEnabled = true });
        var subject = new DeviceApprovalPortal(configuration.Object, _trust.Object, sessions.Object, users.Object, _time);

        var first = await subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        await subject.SelectAsync(first.Id, user.Id, "direct", false);
        await subject.ConfirmAsync(first.Id, user.Id, "direct", true, false, false);
        _trust.Verify(x => x.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<DateTime?>()), Times.Never);

        var second = await subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        await subject.SelectAsync(second.Id, user.Id, "direct", false);
        await subject.ConfirmAsync(second.Id, user.Id, "direct", true, true, false);
        _trust.Verify(x => x.IssueAsync(user.Id, It.IsAny<string>(), "installation-a", "Portal", user.Id, false, null), Times.Once);
    }

    [Fact]
    public async Task ExpiredRequest_CannotBeSelected()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        var id = Assert.Single(_subject.GetQueue(false)).Id;
        _time.Advance(TimeSpan.FromMinutes(5));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _subject.SelectAsync(id, Guid.NewGuid(), "direct", false));
        Assert.Empty(_subject.GetQueue(false));
    }

    [Fact]
    public async Task DisconnectedRequest_IsRemovedAfterHeartbeatLease()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        _time.Advance(TimeSpan.FromSeconds(11));

        Assert.Empty(_subject.GetQueue(false));
    }

    [Fact]
    public async Task CanceledRequestSecret_CannotBeReplayed()
    {
        var request = await _subject.InitiateAsync(_client, Request(), "192.0.2.10", "jellyfin.example.test");
        await _subject.CancelAsync(request.RequestSecret!);

        Assert.Throws<ResourceNotFoundException>(() => _subject.GetStatus(request.RequestSecret!));
        Assert.Empty(_subject.GetQueue(false));
    }

    private static DeviceApprovalInitiateRequest Request() => new() { DeviceCredential = new string('x', 43), Platform = "Linux", OsVersion = "6.1" };

    private async Task<bool> TrySelect(string id, Guid actor, string token)
    {
        try { await _subject.SelectAsync(id, actor, token, false); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
