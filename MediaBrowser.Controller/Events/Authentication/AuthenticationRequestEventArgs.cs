using System;
using MediaBrowser.Controller.Session;

namespace MediaBrowser.Controller.Events.Authentication;

/// <summary>
/// A class representing an authentication result event.
/// </summary>
public class AuthenticationRequestEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequestEventArgs"/> class.
    /// </summary>
    /// <param name="request">The <see cref="AuthenticationRequest"/>.</param>
    public AuthenticationRequestEventArgs(AuthenticationRequest request)
    {
        Username = request.Username;
        UserId = request.UserId;
        App = request.App;
        AppVersion = request.AppVersion;
        DeviceId = request.DeviceId;
        DeviceName = request.DeviceName;
        RemoteEndPoint = request.RemoteEndPoint;
        RequestHost = request.RequestHost;
        RequestScheme = request.RequestScheme;
        ForwardedFor = request.ForwardedFor;
        ForwardedHost = request.ForwardedHost;
        ForwardedProto = request.ForwardedProto;
        CfConnectingIp = request.CfConnectingIp;
        CfConnectingIpv6 = request.CfConnectingIpv6;
        TrueClientIp = request.TrueClientIp;
        OriginalHost = request.OriginalHost;
        UserAgent = request.UserAgent;
    }

    /// <summary>
    /// Gets or sets the user name.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Gets or sets the app.
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// Gets or sets the app version.
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// Gets or sets the device id.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the device name.
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Gets or sets the remote endpoint.
    /// </summary>
    public string? RemoteEndPoint { get; set; }

    /// <summary>
    /// Gets or sets the request host.
    /// </summary>
    public string? RequestHost { get; set; }

    /// <summary>
    /// Gets or sets the request scheme.
    /// </summary>
    public string? RequestScheme { get; set; }

    /// <summary>
    /// Gets or sets the X-Forwarded-For header.
    /// </summary>
    public string? ForwardedFor { get; set; }

    /// <summary>
    /// Gets or sets the X-Forwarded-Host header.
    /// </summary>
    public string? ForwardedHost { get; set; }

    /// <summary>
    /// Gets or sets the X-Forwarded-Proto header.
    /// </summary>
    public string? ForwardedProto { get; set; }

    /// <summary>
    /// Gets or sets the CF-Connecting-IP header.
    /// </summary>
    public string? CfConnectingIp { get; set; }

    /// <summary>
    /// Gets or sets the CF-Connecting-IPv6 header.
    /// </summary>
    public string? CfConnectingIpv6 { get; set; }

    /// <summary>
    /// Gets or sets the True-Client-IP header.
    /// </summary>
    public string? TrueClientIp { get; set; }

    /// <summary>
    /// Gets or sets the X-Original-Host header.
    /// </summary>
    public string? OriginalHost { get; set; }

    /// <summary>
    /// Gets or sets the User-Agent header.
    /// </summary>
    public string? UserAgent { get; set; }
}
