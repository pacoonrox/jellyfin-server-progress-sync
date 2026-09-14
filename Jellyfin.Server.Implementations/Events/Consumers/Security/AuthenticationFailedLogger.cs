using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Authentication;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Events.Consumers.Security
{
    /// <summary>
    /// Creates an entry in the activity log when there is a failed login attempt.
    /// </summary>
    public class AuthenticationFailedLogger : IEventConsumer<AuthenticationRequestEventArgs>
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IActivityManager _activityManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationFailedLogger"/> class.
        /// </summary>
        /// <param name="localizationManager">The localization manager.</param>
        /// <param name="activityManager">The activity manager.</param>
        public AuthenticationFailedLogger(ILocalizationManager localizationManager, IActivityManager activityManager)
        {
            _localizationManager = localizationManager;
            _activityManager = activityManager;
        }

        private static string? AddPart(string? value, string name)
        {
            return string.IsNullOrWhiteSpace(value) ? null : $"{name}: {value}";
        }

        private static string? FirstForwardedIp(string? value)
        {
            return value?.Split(',').Select(part => part.Trim()).FirstOrDefault(part => !string.IsNullOrWhiteSpace(part));
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string GetDisplayRemoteEndPoint(AuthenticationRequestEventArgs eventArgs)
        {
            return FirstNonEmpty(
                eventArgs.CfConnectingIp,
                eventArgs.CfConnectingIpv6,
                eventArgs.TrueClientIp,
                FirstForwardedIp(eventArgs.ForwardedFor),
                eventArgs.RemoteEndPoint)
                ?? "unknown";
        }

        private static string GetOverview(AuthenticationRequestEventArgs eventArgs)
        {
            return string.Join(
                "; ",
                new[]
                {
                    AddPart(eventArgs.RemoteEndPoint, "Proxy-IP"),
                    AddPart(eventArgs.DeviceName, "Device"),
                    AddPart(eventArgs.App, "App"),
                    AddPart(eventArgs.AppVersion, "App-Version"),
                    AddPart(eventArgs.UserAgent, "User-Agent"),
                    AddPart(eventArgs.RequestHost, "Host"),
                    AddPart(eventArgs.RequestScheme, "Scheme"),
                    AddPart(eventArgs.OriginalHost, "X-Original-Host"),
                    AddPart(eventArgs.ForwardedHost, "X-Forwarded-Host"),
                    AddPart(eventArgs.ForwardedProto, "X-Forwarded-Proto"),
                    AddPart(eventArgs.ForwardedFor, "X-Forwarded-For"),
                    AddPart(eventArgs.CfConnectingIp, "CF-Connecting-IP"),
                    AddPart(eventArgs.CfConnectingIpv6, "CF-Connecting-IPv6"),
                    AddPart(eventArgs.TrueClientIp, "True-Client-IP")
                }.OfType<string>());
        }

        /// <inheritdoc />
        public async Task OnEvent(AuthenticationRequestEventArgs eventArgs)
        {
            await _activityManager.CreateAsync(new ActivityLog(
                string.Format(
                    CultureInfo.InvariantCulture,
                    _localizationManager.GetServerLocalizedString("FailedLoginAttemptWithUserName"),
                    eventArgs.Username),
                "AuthenticationFailed",
                Guid.Empty)
            {
                LogSeverity = LogLevel.Error,
                Overview = GetOverview(eventArgs),
                ShortOverview = string.Format(
                    CultureInfo.InvariantCulture,
                    _localizationManager.GetServerLocalizedString("LabelIpAddressValue"),
                    GetDisplayRemoteEndPoint(eventArgs)),
            }).ConfigureAwait(false);
        }
    }
}
