#pragma warning disable CS1591, SA1600

namespace Jellyfin.Database.Implementations.Enums;

public enum InactiveLogoutScope
{
    AllDevices = 0,
    NoDevices = 1,
    AllDevicesExceptSelected = 2,
    NoDevicesExceptSelected = 3,
    SelectedManual = 4
}
