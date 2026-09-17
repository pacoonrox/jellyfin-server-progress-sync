using System;

namespace MediaBrowser.Controller.DeviceApproval;

/// <summary>Defines which authenticated clients belong in the managed device inventory.</summary>
public static class DeviceInventoryPolicy
{
    /// <summary>Returns whether a client should be represented in trusted-device administration.</summary>
    /// <param name="appName">The authenticated client's application name.</param>
    /// <returns><see langword="true"/> when the client belongs in the device inventory.</returns>
    public static bool ShouldTrack(string? appName)
        => appName?.Contains("seerr", StringComparison.OrdinalIgnoreCase) != true;
}
