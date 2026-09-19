using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.DeviceApproval;

#pragma warning disable CS1591, SA1516

namespace MediaBrowser.Controller.DeviceApproval;

public interface IDeviceApprovalPortal
{
    bool IsEnabled { get; }
    Task<DeviceApprovalRequestDto> InitiateAsync(AuthorizationInfo client, DeviceApprovalInitiateRequest request, string ipAddress, string connectionDomain);
    DeviceApprovalRequestDto GetStatus(string requestSecret);
    IReadOnlyList<DeviceApprovalRequestDto> GetQueue(bool includeIpAddress);
    Task<DeviceApprovalRequestDto> SelectAsync(string requestId, Guid actorUserId, string actorAccessToken, bool isApiKey);
    Task<AuthenticationResult?> ConfirmAsync(string requestId, Guid actorUserId, string actorAccessToken, bool matches, bool trustDevice, bool isApiKey);
    Task CancelAsync(string requestSecret);
    Task DenyAsync(string requestId, Guid actorUserId);
}
