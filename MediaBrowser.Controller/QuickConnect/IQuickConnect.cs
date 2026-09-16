using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.QuickConnect;

#pragma warning disable CS1591

namespace MediaBrowser.Controller.QuickConnect;

/// <summary>Provides the legacy Quick Connect protocol used by native clients.</summary>
public interface IQuickConnect
{
    bool IsEnabled { get; }

    QuickConnectResult TryConnect(AuthorizationInfo authorizationInfo);

    QuickConnectResult CheckRequestStatus(string secret);

    Task<bool> AuthorizeRequest(Guid userId, string code);

    AuthenticationResult GetAuthorizedRequest(string secret);
}
