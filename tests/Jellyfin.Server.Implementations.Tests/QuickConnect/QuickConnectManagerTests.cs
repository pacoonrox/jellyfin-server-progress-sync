using System;
using System.Threading.Tasks;
using Emby.Server.Implementations.QuickConnect;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Configuration;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.QuickConnect;

public sealed class QuickConnectManagerTests
{
    private static readonly AuthorizationInfo Client = new()
    {
        Device = "Living Room Roku",
        DeviceId = "roku-installation",
        Client = "Jellyfin Roku",
        Version = "3.2.0"
    };

    private readonly ServerConfiguration _configuration = new() { DeviceApprovalAvailable = true };
    private readonly Mock<ISessionManager> _sessions = new();
    private readonly QuickConnectManager _subject;

    public QuickConnectManagerTests()
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(x => x.Configuration).Returns(_configuration);
        _sessions.Setup(x => x.AuthenticatePortalSession(It.IsAny<AuthenticationRequest>())).ReturnsAsync(new AuthenticationResult { AccessToken = "token" });
        _subject = new QuickConnectManager(configuration.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<QuickConnectManager>>(), _sessions.Object);
    }

    [Fact]
    public void DisabledPortal_DisablesLegacyQuickConnect()
    {
        _configuration.DeviceApprovalAvailable = false;
        Assert.False(_subject.IsEnabled);
        Assert.Throws<AuthenticationException>(() => _subject.TryConnect(Client));
    }

    [Theory]
    [InlineData("", "installation", "client", "1.0")]
    [InlineData("device", "", "client", "1.0")]
    [InlineData("device", "installation", "", "1.0")]
    [InlineData("device", "installation", "client", "")]
    public void Initiate_RejectsMissingClientIdentity(string device, string deviceId, string client, string version)
    {
        Assert.Throws<ArgumentException>(() => _subject.TryConnect(new AuthorizationInfo { Device = device, DeviceId = deviceId, Client = client, Version = version }));
    }

    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    [InlineData("22222222-2222-2222-2222-222222222222")]
    public async Task CompleteFlow_AuthenticatesAnyAuthorizedUserRole(string userId)
    {
        var request = _subject.TryConnect(Client);

        Assert.Matches("^[0-9]{6}$", request.Code);
        Assert.False(_subject.CheckRequestStatus(request.Secret).Authenticated);
        Assert.True(await _subject.AuthorizeRequest(Guid.Parse(userId), request.Code));
        Assert.True(_subject.CheckRequestStatus(request.Secret).Authenticated);
        Assert.Equal("token", _subject.GetAuthorizedRequest(request.Secret).AccessToken);
        _sessions.Verify(x => x.AuthenticatePortalSession(It.Is<AuthenticationRequest>(r => r.UserId.Equals(Guid.Parse(userId)))), Times.Once);
    }

    [Fact]
    public void UnknownSecret_IsRejected()
    {
        Assert.Throws<ResourceNotFoundException>(() => _subject.CheckRequestStatus("unknown"));
        Assert.Throws<ResourceNotFoundException>(() => _subject.GetAuthorizedRequest("unknown"));
    }
}
