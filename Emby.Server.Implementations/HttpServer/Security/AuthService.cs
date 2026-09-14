#pragma warning disable CS1591

using System;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Http;

namespace Emby.Server.Implementations.HttpServer.Security
{
    public class AuthService : IAuthService
    {
        private readonly IAuthorizationContext _authorizationContext;

        public AuthService(
            IAuthorizationContext authorizationContext)
        {
            _authorizationContext = authorizationContext;
        }

        public async Task<AuthorizationInfo> Authenticate(HttpRequest request)
        {
            var auth = await _authorizationContext.GetAuthorizationInfo(request).ConfigureAwait(false);

            if (!auth.HasToken)
            {
                return auth;
            }

            if (!auth.IsAuthenticated)
            {
                throw new SecurityException("Invalid token.");
            }

            if (auth.User?.HasPermission(PermissionKind.IsDisabled) ?? false)
            {
                throw new SecurityException("User account has been disabled.");
            }

            if (!auth.IsApiKey
                && auth.User is not null
                && auth.User.GetTwoFactorAuthenticationPolicy() == TwoFactorAuthenticationPolicy.Required
                && !auth.User.IsTwoFactorAuthenticationEnabled()
                && !IsAllowedBeforeTwoFactorSetup(request, auth.User.Id))
            {
                throw new SecurityException("Two-factor authentication setup is required.");
            }

            return auth;
        }

        private static bool IsAllowedBeforeTwoFactorSetup(HttpRequest request, Guid userId)
        {
            var path = request.Path.Value?.Trim('/') ?? string.Empty;
            if (path.Equals("Users/Me", StringComparison.OrdinalIgnoreCase)
                || path.Equals("Sessions/Logout", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 3
                && segments[0].Equals("Users", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(segments[1], out var routeUserId)
                && routeUserId.Equals(userId)
                && segments[2].Equals("TwoFactor", StringComparison.OrdinalIgnoreCase);
        }
    }
}
