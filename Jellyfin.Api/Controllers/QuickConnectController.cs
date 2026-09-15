using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>Compatibility response for clients using the removed Quick Connect protocol.</summary>
[Route("QuickConnect")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class QuickConnectController : BaseJellyfinApiController
{
    [HttpGet("Enabled")]
    public ActionResult<bool> Enabled() => false;

    [HttpPost("Initiate")]
    [HttpGet("Connect")]
    [HttpPost("Authorize")]
    public ActionResult Removed()
        => StatusCode(StatusCodes.Status410Gone, "Quick Connect has been replaced by DeviceApproval; update this client to use the shared portal.");
}
