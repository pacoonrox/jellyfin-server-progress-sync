using System;
using System.Linq;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public sealed class DeviceApprovalControllerAuthorizationTests
{
    [Theory]
    [InlineData(nameof(DeviceApprovalController.Policy))]
    [InlineData(nameof(DeviceApprovalController.UpdatePolicy))]
    [InlineData(nameof(DeviceApprovalController.Devices))]
    [InlineData(nameof(DeviceApprovalController.Audit))]
    [InlineData(nameof(DeviceApprovalController.Update))]
    [InlineData(nameof(DeviceApprovalController.Trust))]
    [InlineData(nameof(DeviceApprovalController.Revoke))]
    [InlineData(nameof(DeviceApprovalController.NeverTrust))]
    [InlineData(nameof(DeviceApprovalController.Prune))]
    [InlineData(nameof(DeviceApprovalController.RevokeUser))]
    [InlineData(nameof(DeviceApprovalController.RevokeAll))]
    [InlineData(nameof(DeviceApprovalController.LogoutDevice))]
    [InlineData(nameof(DeviceApprovalController.LogoutUserDevices))]
    [InlineData(nameof(DeviceApprovalController.LogoutAllUsersDevices))]
    public void TrustedDeviceManagement_IsAdministratorOnly(string methodName)
    {
        var method = typeof(DeviceApprovalController).GetMethods().Single(x => x.Name == methodName);
        var authorize = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(UserRoles.Administrator, authorize.Roles);
    }

    [Theory]
    [InlineData(nameof(DeviceApprovalController.Queue))]
    [InlineData(nameof(DeviceApprovalController.PortalEntered))]
    [InlineData(nameof(DeviceApprovalController.Select))]
    [InlineData(nameof(DeviceApprovalController.Confirm))]
    [InlineData(nameof(DeviceApprovalController.Deny))]
    public void SharedQueue_RequiresAuthentication(string methodName)
    {
        var method = typeof(DeviceApprovalController).GetMethods().Single(x => x.Name == methodName);
        Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true));
    }

    [Fact]
    public void LegacyQuickConnectAuthorize_AllowsAnyAuthenticatedRole()
    {
        var method = typeof(QuickConnectController).GetMethods().Single(x => x.Name == nameof(QuickConnectController.Authorize));
        var authorize = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.True(string.IsNullOrEmpty(authorize.Roles));
    }
}
