using System;

#pragma warning disable CS1591

namespace MediaBrowser.Model.QuickConnect;

/// <summary>Stores the state of a legacy Quick Connect request.</summary>
public sealed class QuickConnectResult
{
    public QuickConnectResult(string secret, string code, DateTime dateAdded, string deviceId, string deviceName, string appName, string appVersion)
    {
        Secret = secret;
        Code = code;
        DateAdded = dateAdded;
        DeviceId = deviceId;
        DeviceName = deviceName;
        AppName = appName;
        AppVersion = appVersion;
    }

    public bool Authenticated { get; set; }

    public string Secret { get; }

    public string Code { get; }

    public string DeviceId { get; }

    public string DeviceName { get; }

    public string AppName { get; }

    public string AppVersion { get; }

    public DateTime DateAdded { get; set; }
}
