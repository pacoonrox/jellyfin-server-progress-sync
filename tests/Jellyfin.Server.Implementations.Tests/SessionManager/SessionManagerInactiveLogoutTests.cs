using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Queries;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Entities.Security;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.DeviceApproval;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.SessionManager;

public sealed class SessionManagerInactiveLogoutTests
{
    [Theory]
    [InlineData(InactiveLogoutScope.Device, false, false, 1, true)]
    [InlineData(InactiveLogoutScope.User, false, false, 2, true)]
    [InlineData(InactiveLogoutScope.User, true, false, 2, true)]
    [InlineData(InactiveLogoutScope.UserExceptDevice, false, false, 1, false)]
    [InlineData(InactiveLogoutScope.User, false, true, 0, false)]
    public async Task LogoutInactive_AppliesConfiguredScope_ForRegularAndAdministratorUsers(
        InactiveLogoutScope scope,
        bool isAdministrator,
        bool isAdministratorTrusted,
        int expectedLogoutCount,
        bool expectInactiveDeviceLogout)
    {
        var user = new User("idle-user", "default", "default");
        user.SetPermission(PermissionKind.IsAdministrator, isAdministrator);

        var inactiveDevice = new Device(user.Id, "Web", "1.0", "Inactive device", "inactive-device");
        var otherDevice = new Device(user.Id, "Web", "1.0", "Other device", "other-device");
        var devices = new[] { inactiveDevice, otherDevice };
        var loggedOutDevices = new List<Device>();

        var deviceManager = new Mock<IDeviceManager>();
        deviceManager
            .Setup(manager => manager.GetDevices(It.IsAny<DeviceQuery>()))
            .Returns((DeviceQuery query) =>
            {
                IEnumerable<Device> matchingDevices = devices;
                if (query.UserId.HasValue)
                {
                    matchingDevices = matchingDevices.Where(device => device.UserId.Equals(query.UserId.Value));
                }

                if (!string.IsNullOrEmpty(query.DeviceId))
                {
                    matchingDevices = matchingDevices.Where(device => device.DeviceId == query.DeviceId);
                }

                if (!string.IsNullOrEmpty(query.AccessToken))
                {
                    matchingDevices = matchingDevices.Where(device => device.AccessToken == query.AccessToken);
                }

                return new QueryResult<Device>(matchingDevices.ToList());
            });
        deviceManager
            .Setup(manager => manager.DeleteDevice(It.IsAny<Device>()))
            .Callback<Device>(loggedOutDevices.Add)
            .Returns(Task.CompletedTask);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);

        var trustedDeviceManager = new Mock<ITrustedDeviceManager>();
        trustedDeviceManager
            .Setup(manager => manager.IsAdministratorTrustedAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(isAdministratorTrusted);
        trustedDeviceManager
            .Setup(manager => manager.RequireFreshTwoFactorAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var hostLifetime = new Mock<IHostApplicationLifetime>();
        hostLifetime.SetupGet(host => host.ApplicationStopping).Returns(CancellationToken.None);

        var serverConfigurationManager = new Mock<IServerConfigurationManager>();
        serverConfigurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration
        {
            InactiveLogoutMinutes = 15,
            InactiveLogoutScope = scope
        });

        await using var sessionManager = new Emby.Server.Implementations.Session.SessionManager(
            NullLogger<Emby.Server.Implementations.Session.SessionManager>.Instance,
            Mock.Of<IEventManager>(),
            Mock.Of<IUserDataManager>(),
            serverConfigurationManager.Object,
            Mock.Of<ILibraryManager>(),
            userManager.Object,
            Mock.Of<IMusicManager>(),
            Mock.Of<IDtoService>(),
            Mock.Of<IImageProcessor>(),
            Mock.Of<IServerApplicationHost>(),
            deviceManager.Object,
            trustedDeviceManager.Object,
            Mock.Of<IMediaSourceManager>(),
            hostLifetime.Object);

        var logoutCurrentDevice = await sessionManager.LogoutInactive(inactiveDevice.AccessToken);

        Assert.Equal(expectInactiveDeviceLogout, logoutCurrentDevice);
        Assert.Equal(expectedLogoutCount, loggedOutDevices.Count);
        Assert.Equal(expectInactiveDeviceLogout, loggedOutDevices.Contains(inactiveDevice));
        Assert.Equal(expectedLogoutCount > 0 && scope != InactiveLogoutScope.Device, loggedOutDevices.Contains(otherDevice));
        Assert.Equal(
            expectedLogoutCount,
            trustedDeviceManager.Invocations.Count(invocation => invocation.Method.Name == nameof(ITrustedDeviceManager.RequireFreshTwoFactorAsync)));
    }

    [Fact]
    public void InactiveLogoutPolicy_DefaultsToDisabledAndDeviceScope()
    {
        var configuration = new ServerConfiguration();

        Assert.Equal(0, configuration.InactiveLogoutMinutes);
        Assert.Equal(InactiveLogoutScope.Device, configuration.InactiveLogoutScope);
    }
}
