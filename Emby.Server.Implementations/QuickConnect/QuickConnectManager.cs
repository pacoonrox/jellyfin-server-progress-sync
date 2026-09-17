using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.QuickConnect;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.QuickConnect;
using Microsoft.Extensions.Logging;

#pragma warning disable CS1591

namespace Emby.Server.Implementations.QuickConnect;

/// <summary>Compatibility implementation for clients that use Jellyfin Quick Connect.</summary>
public sealed class QuickConnectManager : IQuickConnect
{
    private const int CodeLength = 6;
    private const int TimeoutMinutes = 10;
    private readonly ConcurrentDictionary<string, QuickConnectResult> _currentRequests = new();
    private readonly ConcurrentDictionary<string, (DateTime Timestamp, AuthenticationResult Result)> _authorizedSecrets = new();
    private readonly IServerConfigurationManager _configuration;
    private readonly ILogger<QuickConnectManager> _logger;
    private readonly ISessionManager _sessions;

    public QuickConnectManager(IServerConfigurationManager configuration, ILogger<QuickConnectManager> logger, ISessionManager sessions)
    {
        _configuration = configuration;
        _logger = logger;
        _sessions = sessions;
    }

    // Legacy compatibility follows the shared portal switch so existing
    // installations do not need a second setting enabled after upgrading.
    public bool IsEnabled => _configuration.Configuration.DeviceApprovalAvailable;

    public QuickConnectResult TryConnect(AuthorizationInfo authorizationInfo)
    {
        ArgumentException.ThrowIfNullOrEmpty(authorizationInfo.DeviceId);
        ArgumentException.ThrowIfNullOrEmpty(authorizationInfo.Device);
        ArgumentException.ThrowIfNullOrEmpty(authorizationInfo.Client);
        ArgumentException.ThrowIfNullOrEmpty(authorizationInfo.Version);
        AssertActive();
        ExpireRequests();

        string code;
        do
        {
            code = RandomNumberGenerator.GetInt32((int)Math.Pow(10, CodeLength - 1), (int)Math.Pow(10, CodeLength)).ToString(CultureInfo.InvariantCulture);
        }
        while (_currentRequests.ContainsKey(code));

        var result = new QuickConnectResult(
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            code,
            DateTime.UtcNow,
            authorizationInfo.DeviceId,
            authorizationInfo.Device,
            authorizationInfo.Client,
            authorizationInfo.Version);
        _currentRequests[code] = result;
        return result;
    }

    public QuickConnectResult CheckRequestStatus(string secret)
    {
        AssertActive();
        ExpireRequests();
        return _currentRequests.Values.FirstOrDefault(x => string.Equals(x.Secret, secret, StringComparison.Ordinal))
            ?? throw new ResourceNotFoundException("Unable to find request with provided secret");
    }

    public async Task<bool> AuthorizeRequest(Guid userId, string code)
    {
        AssertActive();
        ExpireRequests();
        if (!_currentRequests.TryGetValue(code, out var request))
        {
            throw new ResourceNotFoundException("Unable to find request");
        }

        if (request.Authenticated)
        {
            throw new InvalidOperationException("Request is already authorized");
        }

        var result = await _sessions.AuthenticatePortalSession(new AuthenticationRequest
        {
            UserId = userId,
            DeviceId = request.DeviceId,
            DeviceName = request.DeviceName,
            App = request.AppName,
            AppVersion = request.AppVersion,
            DeviceCredential = request.Secret,
            Platform = "Legacy Quick Connect"
        }).ConfigureAwait(false);

        request.DateAdded = DateTime.UtcNow.AddMinutes(1);
        request.Authenticated = true;
        _authorizedSecrets[request.Secret] = (DateTime.UtcNow, result);
        _logger.LogDebug("Authorizing legacy Quick Connect device with code {Code} for user {UserId}", code, userId);
        return true;
    }

    public AuthenticationResult GetAuthorizedRequest(string secret)
    {
        AssertActive();
        ExpireRequests();
        return _authorizedSecrets.TryGetValue(secret, out var result)
            ? result.Result
            : throw new ResourceNotFoundException("Unable to find request");
    }

    private void AssertActive()
    {
        if (!IsEnabled)
        {
            throw new AuthenticationException("Quick Connect is not active on this server");
        }
    }

    private void ExpireRequests()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-TimeoutMinutes);
        foreach (var request in _currentRequests.Where(x => x.Value.DateAdded < cutoff).ToArray())
        {
            _currentRequests.TryRemove(request.Key, out _);
        }

        foreach (var result in _authorizedSecrets.Where(x => x.Value.Timestamp < cutoff).ToArray())
        {
            _authorizedSecrets.TryRemove(result.Key, out _);
        }
    }
}
