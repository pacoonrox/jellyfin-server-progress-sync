#pragma warning disable CS1591, SA1600

namespace Jellyfin.Database.Implementations.Enums;

public enum TwoFactorAuthenticationPolicy
{
    Disabled = 0,
    Allowed = 1,
    Required = 2
}
