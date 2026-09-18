namespace Jellyfin.Database.Implementations.Enums;

/// <summary>
/// The types of user preferences.
/// </summary>
public enum PreferenceKind
{
    /// <summary>
    /// A list of blocked tags.
    /// </summary>
    BlockedTags = 0,

    /// <summary>
    /// A list of blocked channels.
    /// </summary>
    BlockedChannels = 1,

    /// <summary>
    /// A list of blocked media folders.
    /// </summary>
    BlockedMediaFolders = 2,

    /// <summary>
    /// A list of enabled devices.
    /// </summary>
    EnabledDevices = 3,

    /// <summary>
    /// A list of enabled channels.
    /// </summary>
    EnabledChannels = 4,

    /// <summary>
    /// A list of enabled folders.
    /// </summary>
    EnabledFolders = 5,

    /// <summary>
    /// A list of folders to allow content deletion from.
    /// </summary>
    EnableContentDeletionFromFolders = 6,

    /// <summary>
    /// A list of latest items to exclude.
    /// </summary>
    LatestItemExcludes = 7,

    /// <summary>
    /// A list of media to exclude.
    /// </summary>
    MyMediaExcludes = 8,

    /// <summary>
    /// A list of grouped folders.
    /// </summary>
    GroupedFolders = 9,

    /// <summary>
    /// A list of unrated items to block.
    /// </summary>
    BlockUnratedItems = 10,

    /// <summary>
    /// A list of ordered views.
    /// </summary>
    OrderedViews = 11,

    /// <summary>
    /// A list of allowed tags.
    /// </summary>
    AllowedTags = 12,

    /// <summary>
    /// The user's two-factor authentication policy.
    /// </summary>
    TwoFactorAuthenticationPolicy = 13,

    /// <summary>
    /// The user's active two-factor authentication secret.
    /// </summary>
    TwoFactorAuthenticationSecret = 14,

    /// <summary>
    /// The user's pending two-factor authentication setup secret.
    /// </summary>
    TwoFactorAuthenticationPendingSecret = 15,

    /// <summary>
    /// Whether two-factor authentication has been registered by the user.
    /// </summary>
    TwoFactorAuthenticationEnabled = 16,

    /// <summary>
    /// The user's two-factor authentication registration timestamp.
    /// </summary>
    TwoFactorAuthenticationRegisteredDate = 17,

    /// <summary>
    /// Failed two-factor authentication attempts for the user.
    /// </summary>
    TwoFactorAuthenticationFailedAttemptCount = 18,

    /// <summary>
    /// The user's automatic logout timeout in inactive minutes.
    /// </summary>
    InactiveLogoutMinutes = 19,

    /// <summary>
    /// Whether an automatic inactivity logout applies to one device or every device for the user.
    /// </summary>
    InactiveLogoutScope = 20
}
