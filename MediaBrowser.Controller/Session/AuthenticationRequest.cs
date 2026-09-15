#nullable disable

#pragma warning disable CS1591

using System;
using System.Linq;

namespace MediaBrowser.Controller.Session
{
    public class AuthenticationRequest
    {
        public string Username { get; set; }

        public Guid UserId { get; set; }

        public string Password { get; set; }

        public string TwoFactorCode { get; set; }

        public string DeviceCredential { get; set; }

        public bool TrustDevice { get; set; }

        public string Platform { get; set; }

        public string OsVersion { get; set; }

        [Obsolete("Send full password in Password field")]
        public string PasswordSha1 { get; set; }

        public string App { get; set; }

        public string AppVersion { get; set; }

        public string DeviceId { get; set; }

        public string DeviceName { get; set; }

        public string RemoteEndPoint { get; set; }

        public string RequestHost { get; set; }

        public string RequestScheme { get; set; }

        public string ForwardedFor { get; set; }

        public string ForwardedHost { get; set; }

        public string ForwardedProto { get; set; }

        public string CfConnectingIp { get; set; }

        public string CfConnectingIpv6 { get; set; }

        public string TrueClientIp { get; set; }

        public string OriginalHost { get; set; }

        public string UserAgent { get; set; }

        public string GetAuthFailureSource()
        {
            var parts = new[]
            {
                AddPart(RemoteEndPoint, "IP"),
                AddPart(DeviceName, "Device"),
                AddPart(App, "App"),
                AddPart(AppVersion, "App-Version"),
                AddPart(UserAgent, "User-Agent"),
                AddPart(RequestHost, "Host"),
                AddPart(ForwardedHost, "X-Forwarded-Host"),
                AddPart(OriginalHost, "X-Original-Host"),
                AddPart(ForwardedFor, "X-Forwarded-For"),
                AddPart(CfConnectingIp, "CF-Connecting-IP"),
                AddPart(TrueClientIp, "True-Client-IP")
            };

            return string.Join("; ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string AddPart(string value, string name)
        {
            return string.IsNullOrWhiteSpace(value) ? null : $"{name}: {value}";
        }
    }
}
