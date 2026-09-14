using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Database.Implementations.Interfaces;

namespace Jellyfin.Data;

/// <summary>
/// Contains extension methods for manipulation of <see cref="User"/> entities.
/// </summary>
public static class UserEntityExtensions
{
    /// <summary>
    /// The values being delimited here are Guids, so commas work as they do not appear in Guids.
    /// </summary>
    private const char Delimiter = ',';

    /// <summary>
    /// Checks whether the user has the specified permission.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="kind">The permission kind.</param>
    /// <returns><c>True</c> if the user has the specified permission.</returns>
    public static bool HasPermission(this IHasPermissions entity, PermissionKind kind)
    {
        return entity.Permissions.FirstOrDefault(p => p.Kind == kind)?.Value ?? false;
    }

    /// <summary>
    /// Sets the given permission kind to the provided value.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="kind">The permission kind.</param>
    /// <param name="value">The value to set.</param>
    public static void SetPermission(this IHasPermissions entity, PermissionKind kind, bool value)
    {
        var currentPermission = entity.Permissions.FirstOrDefault(p => p.Kind == kind);
        if (currentPermission is null)
        {
            entity.Permissions.Add(new Permission(kind, value));
        }
        else
        {
            currentPermission.Value = value;
        }
    }

    /// <summary>
    /// Gets the user's preferences for the given preference kind.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="preference">The preference kind.</param>
    /// <returns>A string array containing the user's preferences.</returns>
    public static string[] GetPreference(this User entity, PreferenceKind preference)
    {
        var val = entity.Preferences.FirstOrDefault(p => p.Kind == preference)?.Value;

        return string.IsNullOrEmpty(val) ? Array.Empty<string>() : val.Split(Delimiter);
    }

    /// <summary>
    /// Gets the user's preferences for the given preference kind.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="preference">The preference kind.</param>
    /// <typeparam name="T">Type of preference.</typeparam>
    /// <returns>A {T} array containing the user's preference.</returns>
    public static T[] GetPreferenceValues<T>(this User entity, PreferenceKind preference)
    {
        var val = entity.Preferences.FirstOrDefault(p => p.Kind == preference)?.Value;
        if (string.IsNullOrEmpty(val))
        {
            return Array.Empty<T>();
        }

        // Convert array of {string} to array of {T}
        var converter = TypeDescriptor.GetConverter(typeof(T));
        var stringValues = val.Split(Delimiter);
        var convertedCount = 0;
        var parsedValues = new T[stringValues.Length];
        for (var i = 0; i < stringValues.Length; i++)
        {
            try
            {
                var parsedValue = converter.ConvertFromString(stringValues[i].Trim());
                if (parsedValue is not null)
                {
                    parsedValues[convertedCount++] = (T)parsedValue;
                }
            }
            catch (FormatException)
            {
                // Unable to convert value
            }
        }

        return parsedValues[..convertedCount];
    }

    /// <summary>
    /// Sets the specified preference to the given value.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="preference">The preference kind.</param>
    /// <param name="values">The values.</param>
    public static void SetPreference(this User entity, PreferenceKind preference, string[] values)
    {
        var value = string.Join(Delimiter, values);
        var currentPreference = entity.Preferences.FirstOrDefault(p => p.Kind == preference);
        if (currentPreference is null)
        {
            entity.Preferences.Add(new Preference(preference, value));
        }
        else
        {
            currentPreference.Value = value;
        }
    }

    /// <summary>
    /// Sets the specified preference to the given value.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="preference">The preference kind.</param>
    /// <param name="values">The values.</param>
    /// <typeparam name="T">The type of value.</typeparam>
    public static void SetPreference<T>(this User entity, PreferenceKind preference, T[] values)
    {
        var value = string.Join(Delimiter, values);
        var currentPreference = entity.Preferences.FirstOrDefault(p => p.Kind == preference);
        if (currentPreference is null)
        {
            entity.Preferences.Add(new Preference(preference, value));
        }
        else
        {
            currentPreference.Value = value;
        }
    }

    /// <summary>
    /// Gets the user's two-factor authentication policy.
    /// </summary>
    /// <param name="entity">The entity to read.</param>
    /// <returns>The two-factor authentication policy.</returns>
    public static TwoFactorAuthenticationPolicy GetTwoFactorAuthenticationPolicy(this User entity)
    {
        var value = entity.GetPreference(PreferenceKind.TwoFactorAuthenticationPolicy).FirstOrDefault();
        return Enum.TryParse<TwoFactorAuthenticationPolicy>(value, true, out var policy)
            ? policy
            : TwoFactorAuthenticationPolicy.Disabled;
    }

    /// <summary>
    /// Sets the user's two-factor authentication policy.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="policy">The two-factor authentication policy.</param>
    public static void SetTwoFactorAuthenticationPolicy(this User entity, TwoFactorAuthenticationPolicy policy)
        => entity.SetPreference(PreferenceKind.TwoFactorAuthenticationPolicy, new[] { policy.ToString() });

    /// <summary>
    /// Gets a two-factor authentication preference value.
    /// </summary>
    /// <param name="entity">The entity to read.</param>
    /// <param name="preference">The preference kind.</param>
    /// <returns>The preference value.</returns>
    public static string? GetTwoFactorAuthenticationValue(this User entity, PreferenceKind preference)
        => entity.GetPreference(preference).FirstOrDefault();

    /// <summary>
    /// Sets a two-factor authentication preference value.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="preference">The preference kind.</param>
    /// <param name="value">The preference value.</param>
    public static void SetTwoFactorAuthenticationValue(this User entity, PreferenceKind preference, string? value)
        => entity.SetPreference(preference, string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value });

    /// <summary>
    /// Gets a value indicating whether two-factor authentication is registered.
    /// </summary>
    /// <param name="entity">The entity to read.</param>
    /// <returns><c>true</c> if two-factor authentication is registered.</returns>
    public static bool IsTwoFactorAuthenticationEnabled(this User entity)
        => bool.TryParse(entity.GetTwoFactorAuthenticationValue(PreferenceKind.TwoFactorAuthenticationEnabled), out var enabled) && enabled;

    /// <summary>
    /// Sets whether two-factor authentication is registered.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="enabled">Whether two-factor authentication is registered.</param>
    public static void SetTwoFactorAuthenticationEnabled(this User entity, bool enabled)
        => entity.SetTwoFactorAuthenticationValue(PreferenceKind.TwoFactorAuthenticationEnabled, enabled.ToString());

    /// <summary>
    /// Gets failed two-factor authentication attempts.
    /// </summary>
    /// <param name="entity">The entity to read.</param>
    /// <returns>The failed attempt count.</returns>
    public static int GetTwoFactorAuthenticationFailedAttemptCount(this User entity)
        => int.TryParse(entity.GetTwoFactorAuthenticationValue(PreferenceKind.TwoFactorAuthenticationFailedAttemptCount), out var count) ? count : 0;

    /// <summary>
    /// Sets failed two-factor authentication attempts.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="count">The failed attempt count.</param>
    public static void SetTwoFactorAuthenticationFailedAttemptCount(this User entity, int count)
        => entity.SetTwoFactorAuthenticationValue(PreferenceKind.TwoFactorAuthenticationFailedAttemptCount, count.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Gets the user's automatic logout timeout in inactive minutes.
    /// </summary>
    /// <param name="entity">The entity to read.</param>
    /// <returns>The automatic logout timeout in inactive minutes.</returns>
    public static int GetInactiveLogoutMinutes(this User entity)
    {
        var value = entity.GetPreference(PreferenceKind.InactiveLogoutMinutes).FirstOrDefault();
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? minutes
            : 0;
    }

    /// <summary>
    /// Sets the user's automatic logout timeout in inactive minutes.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="minutes">The automatic logout timeout in inactive minutes.</param>
    public static void SetInactiveLogoutMinutes(this User entity, int minutes)
        => entity.SetPreference(PreferenceKind.InactiveLogoutMinutes, new[] { Math.Max(0, minutes).ToString(CultureInfo.InvariantCulture) });

    /// <summary>
    /// Checks whether this user is currently allowed to use the server.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns><c>True</c> if the current time is within an access schedule, or there are no access schedules.</returns>
    public static bool IsParentalScheduleAllowed(this User entity)
    {
        return entity.AccessSchedules.Count == 0
               || entity.AccessSchedules.Any(i => IsParentalScheduleAllowed(i, DateTime.UtcNow));
    }

    /// <summary>
    /// Checks whether the provided folder is in this user's grouped folders.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="id">The Guid of the folder.</param>
    /// <returns><c>True</c> if the folder is in the user's grouped folders.</returns>
    public static bool IsFolderGrouped(this User entity, Guid id)
    {
        return Array.IndexOf(GetPreferenceValues<Guid>(entity, PreferenceKind.GroupedFolders), id) != -1;
    }

    /// <summary>
    /// Checks whether any library, parental rating or tag rule keeps content from this user.
    /// </summary>
    /// <param name="entity">The user to check.</param>
    /// <returns><c>True</c> if some content in the library is hidden from this user.</returns>
    public static bool HasContentRestrictions(this User entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return !entity.HasPermission(PermissionKind.EnableAllFolders)
            || entity.GetPreference(PreferenceKind.BlockedMediaFolders).Length > 0
            || entity.MaxParentalRatingScore.HasValue
            || entity.GetPreference(PreferenceKind.BlockedTags).Length > 0
            || entity.GetPreference(PreferenceKind.AllowedTags).Length > 0
            || entity.GetPreference(PreferenceKind.BlockUnratedItems).Length > 0;
    }

    /// <summary>
    /// Initializes the default permissions for a user. Should only be called on user creation.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    // TODO: make these user configurable?
    public static void AddDefaultPermissions(this User entity)
    {
        entity.Permissions.Add(new Permission(PermissionKind.IsAdministrator, false));
        entity.Permissions.Add(new Permission(PermissionKind.IsDisabled, false));
        entity.Permissions.Add(new Permission(PermissionKind.IsHidden, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableAllChannels, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableAllDevices, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableAllFolders, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableContentDeletion, false));
        entity.Permissions.Add(new Permission(PermissionKind.EnableContentDownloading, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableMediaConversion, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableMediaPlayback, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnablePlaybackRemuxing, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnablePublicSharing, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableRemoteAccess, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableSyncTranscoding, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableAudioPlaybackTranscoding, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableLiveTvAccess, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableLiveTvManagement, false));
        entity.Permissions.Add(new Permission(PermissionKind.EnableSharedDeviceControl, true));
        entity.Permissions.Add(new Permission(PermissionKind.EnableVideoPlaybackTranscoding, true));
        entity.Permissions.Add(new Permission(PermissionKind.ForceRemoteSourceTranscoding, false));
        entity.Permissions.Add(new Permission(PermissionKind.EnableRemoteControlOfOtherUsers, false));
        entity.Permissions.Add(new Permission(PermissionKind.EnableCollectionManagement, false));
        entity.Permissions.Add(new Permission(PermissionKind.EnableSubtitleManagement, false));
        entity.Permissions.Add(new Permission(PermissionKind.EnableLyricManagement, false));
    }

    /// <summary>
    /// Initializes the default preferences. Should only be called on user creation.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    public static void AddDefaultPreferences(this User entity)
    {
        foreach (var val in Enum.GetValues<PreferenceKind>())
        {
            entity.Preferences.Add(new Preference(val, string.Empty));
        }
    }

    private static bool IsParentalScheduleAllowed(AccessSchedule schedule, DateTime date)
    {
        var localTime = date.ToLocalTime();
        var hour = localTime.TimeOfDay.TotalHours;
        var currentDayOfWeek = localTime.DayOfWeek;

        return schedule.DayOfWeek.Contains(currentDayOfWeek)
               && hour >= schedule.StartHour
               && hour <= schedule.EndHour;
    }
}
