using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.Infrastructure.Messaging.Outbox;

namespace SellingNewProduct.API.Controllers;

/// <summary>
/// Operational insight into the messaging layer. Reads the in-memory ring buffer
/// (<see cref="IOutboxActivityLog"/>, backed by a ConcurrentQueue) that the outbox dispatcher fills as
/// it publishes — a quick "what has the relay been doing lately?" without touching the database.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public sealed class DiagnosticsController : ControllerBase
{
    private readonly IOutboxActivityLog myActivityLog;

    public DiagnosticsController(IOutboxActivityLog theActivityLog)
    {
        myActivityLog = theActivityLog;
    }

    /// <summary>Most recent outbox dispatch results (newest first).</summary>
    [HttpGet("outbox-activity")]
    public ActionResult<IReadOnlyList<OutboxActivityEntry>> OutboxActivity([FromQuery] int theMax = 50)
    {
        return Ok(myActivityLog.Snapshot(theMax));
    }
}
