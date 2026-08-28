using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;

namespace Emby.Server.Implementations.ProgressSync;

/// <summary>
/// Listens for user progress changes and applies configured sync groups.
/// </summary>
public sealed class ProgressSyncEntryPoint : IHostedService
{
    private readonly IUserDataManager _userDataManager;
    private readonly ProgressSyncManager _progressSyncManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressSyncEntryPoint"/> class.
    /// </summary>
    public ProgressSyncEntryPoint(IUserDataManager userDataManager, ProgressSyncManager progressSyncManager)
    {
        _userDataManager = userDataManager;
        _progressSyncManager = progressSyncManager;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        return Task.CompletedTask;
    }

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
        => _progressSyncManager.HandleUserDataSaved(e);
}
