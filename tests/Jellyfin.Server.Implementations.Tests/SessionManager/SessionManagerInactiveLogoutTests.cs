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
    private const string TargetDeviceId = "target-device";
    private const string OtherDeviceId = "other-device";

    [Theory]
    // Master switch off: never logged out, regardless of scope mode.
    [InlineData(false, InactiveLogoutScope.AllDevices, false, false)]
    // AllDevices: the target device is subject.
    [InlineData(true, InactiveLogoutScope.AllDevices, false, true)]
    // NoDevices: never subject, regardless of the switch.
    [InlineData(true, InactiveLogoutScope.NoDevices, false, false)]
    // AllDevicesExceptSelected, target NOT in the exception list: subject (follows "All").
    [InlineData(true, InactiveLogoutScope.AllDevicesExceptSelected, false, true)]
    // AllDevicesExceptSelected, target IS in the exception list: exempt.
    [InlineData(true, InactiveLogoutScope.AllDevicesExceptSelected, true, false)]
    // NoDevicesExceptSelected, target NOT in the list: exempt (follows "No").
    [InlineData(true, InactiveLogoutScope.NoDevicesExceptSelected, false, false)]
    // NoDevicesExceptSelected, target IS in the list: subject.
    [InlineData(true, InactiveLogoutScope.NoDevicesExceptSelected, true, true)]
    public async Task LogoutInactive_EvaluatesTargetDeviceIndependently_ByScopeMode(
        bool enabled,
        InactiveLogoutScope scope,
        bool targetInSelectedList,
        bool expectLoggedOut)
    {
        var user = new User("idle-user", "default", "default");
        user.SetPermission(PermissionKind.IdleLogoutEnabled, enabled);
        user.SetIdleLogoutMinutes(15);
        user.SetIdleLogoutScopeMode(scope);
        if (targetInSelectedList)
        {
            user.SetPreference(PreferenceKind.IdleLogoutSelectedDeviceIds, new[] { TargetDeviceId });
        }

        var harness = CreateHarness(user, isAdministratorTrusted: false);
        await using var sessionManager = harness.SessionManager;

        var loggedOutCurrentDevice = await sessionManager.LogoutInactive(harness.TargetDevice.AccessToken);

        Assert.Equal(expectLoggedOut, loggedOutCurrentDevice);
        Assert.Equal(expectLoggedOut, harness.LoggedOutDevices.Contains(harness.TargetDevice));
    }

    [Fact]
    public async Task LogoutInactive_OneDeviceGoingIdle_NeverLogsOutASiblingDevice()
    {
        var user = new User("idle-user", "default", "default");
        user.SetPermission(PermissionKind.IdleLogoutEnabled, true);
        user.SetIdleLogoutMinutes(15);
        user.SetIdleLogoutScopeMode(InactiveLogoutScope.AllDevices);

        var harness = CreateHarness(user, isAdministratorTrusted: false);
        await using var sessionManager = harness.SessionManager;

        var loggedOutCurrentDevice = await sessionManager.LogoutInactive(harness.TargetDevice.AccessToken);

        Assert.True(loggedOutCurrentDevice);
        Assert.Contains(harness.TargetDevice, harness.LoggedOutDevices);
        Assert.DoesNotContain(harness.OtherDevice, harness.LoggedOutDevices);
    }

    [Fact]
    public async Task LogoutInactive_SelectedManual_UsesExplicitOverrideWhenPresent()
    {
        var user = new User("idle-user", "default", "default");
        user.SetPermission(PermissionKind.IdleLogoutEnabled, true);
        user.SetIdleLogoutMinutes(15);
        user.SetIdleLogoutScopeMode(InactiveLogoutScope.SelectedManual);
        user.SetPermission(PermissionKind.IdleLogoutManualFutureDefault, false);
        user.IdleLogoutDeviceOverrides.Add(new IdleLogoutDeviceOverride(user.Id, TargetDeviceId, true));

        var harness = CreateHarness(user, isAdministratorTrusted: false);
        await using var sessionManager = harness.SessionManager;

        var loggedOutCurrentDevice = await sessionManager.LogoutInactive(harness.TargetDevice.AccessToken);

        Assert.True(loggedOutCurrentDevice);
        Assert.Contains(harness.TargetDevice, harness.LoggedOutDevices);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task LogoutInactive_SelectedManual_FallsBackToFutureDefault_WhenNoOverrideExists(bool futureDefault, bool expectLoggedOut)
    {
        var user = new User("idle-user", "default", "default");
        user.SetPermission(PermissionKind.IdleLogoutEnabled, true);
        user.SetIdleLogoutMinutes(15);
        user.SetIdleLogoutScopeMode(InactiveLogoutScope.SelectedManual);
        user.SetPermission(PermissionKind.IdleLogoutManualFutureDefault, futureDefault);

        var harness = CreateHarness(user, isAdministratorTrusted: false);
        await using var sessionManager = harness.SessionManager;

        var loggedOutCurrentDevice = await sessionManager.LogoutInactive(harness.TargetDevice.AccessToken);

        Assert.Equal(expectLoggedOut, loggedOutCurrentDevice);
        Assert.Equal(expectLoggedOut, harness.LoggedOutDevices.Contains(harness.TargetDevice));
    }

    [Fact]
    public async Task LogoutInactive_AdministratorTrustedDevice_IsNeverLoggedOut()
    {
        var user = new User("idle-user", "default", "default");
        user.SetPermission(PermissionKind.IdleLogoutEnabled, true);
        user.SetIdleLogoutMinutes(15);
        user.SetIdleLogoutScopeMode(InactiveLogoutScope.AllDevices);

        var harness = CreateHarness(user, isAdministratorTrusted: true);
        await using var sessionManager = harness.SessionManager;

        var loggedOutCurrentDevice = await sessionManager.LogoutInactive(harness.TargetDevice.AccessToken);

        Assert.False(loggedOutCurrentDevice);
        Assert.DoesNotContain(harness.TargetDevice, harness.LoggedOutDevices);
    }

    [Fact]
    public void IdleLogoutDefaults_DisabledWithFiveMinuteAllDevicesScope()
    {
        var user = new User("fresh-user", "default", "default");

        Assert.False(user.IsIdleLogoutEnabled());
        Assert.Equal(5, user.GetIdleLogoutMinutes());
        Assert.Equal(InactiveLogoutScope.AllDevices, user.GetIdleLogoutScopeMode());
    }

    private static (Emby.Server.Implementations.Session.SessionManager SessionManager, Device TargetDevice, Device OtherDevice, List<Device> LoggedOutDevices, Mock<ITrustedDeviceManager> TrustedDeviceManager) CreateHarness(
        User user,
        bool isAdministratorTrusted)
    {
        var targetDevice = new Device(user.Id, "Web", "1.0", "Target device", TargetDeviceId);
        var otherDevice = new Device(user.Id, "Web", "1.0", "Other device", OtherDeviceId);
        var devices = new[] { targetDevice, otherDevice };
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
        serverConfigurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration());

        var sessionManager = new Emby.Server.Implementations.Session.SessionManager(
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

        return (sessionManager, targetDevice, otherDevice, loggedOutDevices, trustedDeviceManager);
    }
}
