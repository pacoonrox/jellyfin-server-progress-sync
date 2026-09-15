using System;
using System.Linq;
using System.Threading.Tasks;
using Emby.Server.Implementations.DeviceApproval;
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
        _trust.Setup(x => x.CanApproveAsync(It.IsAny<string>())).ReturnsAsync(true);
        _trust.Setup(x => x.AuditAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _subject = new DeviceApprovalPortal(configuration.Object, _trust.Object, new Mock<ISessionManager>().Object, new Mock<IUserManager>().Object, _time);
    }

    [Fact]
    public async Task Queue_RedactsIpForRegularUsers()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10");

        Assert.Null(Assert.Single(_subject.GetQueue(false, Guid.NewGuid())).RequestingIpAddress);
        Assert.Equal("192.0.2.10", Assert.Single(_subject.GetQueue(true, Guid.NewGuid())).RequestingIpAddress);
    }

    [Fact]
    public async Task ConcurrentSelection_HasExactlyOneWinner()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10");
        var id = Assert.Single(_subject.GetQueue(false, Guid.NewGuid())).Id;

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
        _trust.Setup(x => x.CanApproveAsync("portal-token")).ReturnsAsync(false);
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10");
        var id = Assert.Single(_subject.GetQueue(false, Guid.NewGuid())).Id;

        await Assert.ThrowsAsync<AuthenticationException>(() => _subject.SelectAsync(id, Guid.NewGuid(), "portal-token"));
    }

    [Fact]
    public async Task Selection_UsesUnpredictableMatchingValueAndNoReturnsToQueue()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10");
        var actor = Guid.NewGuid();
        var id = Assert.Single(_subject.GetQueue(false, actor)).Id;
        var selected = await _subject.SelectAsync(id, actor, "direct");

        Assert.Matches("^[A-Z]+-[A-Z]+-[A-Z]+-[0-9]{3}$", selected.MatchingValue!);
        Assert.Null(await _subject.ConfirmAsync(id, actor, "direct", false, false));
        Assert.Equal(DeviceApprovalState.Pending, Assert.Single(_subject.GetQueue(false, actor)).State);
    }

    [Fact]
    public async Task ExpiredRequest_CannotBeSelected()
    {
        await _subject.InitiateAsync(_client, Request(), "192.0.2.10");
        var id = Assert.Single(_subject.GetQueue(false, Guid.NewGuid())).Id;
        _time.Advance(TimeSpan.FromMinutes(5));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _subject.SelectAsync(id, Guid.NewGuid(), "direct"));
        Assert.Empty(_subject.GetQueue(false, Guid.NewGuid()));
    }

    [Fact]
    public async Task CanceledRequestSecret_CannotBeReplayed()
    {
        var request = await _subject.InitiateAsync(_client, Request(), "192.0.2.10");
        await _subject.CancelAsync(request.RequestSecret!);

        Assert.Throws<ResourceNotFoundException>(() => _subject.GetStatus(request.RequestSecret!));
        Assert.Empty(_subject.GetQueue(false, Guid.NewGuid()));
    }

    private static DeviceApprovalInitiateRequest Request() => new() { DeviceCredential = new string('x', 43), Platform = "Linux", OsVersion = "6.1" };

    private async Task<bool> TrySelect(string id, Guid actor, string token)
    {
        try { await _subject.SelectAsync(id, actor, token); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
