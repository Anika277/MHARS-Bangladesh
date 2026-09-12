using Microsoft.AspNetCore.Mvc;
using MHARS.Web.Models.Agent;
using MHARS.Web.Services;

namespace MHARS.Web.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly IGroqAgentService _agent;

    public AgentController(IGroqAgentService agent)
    {
        _agent = agent;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] AgentChatRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message is required." });

        var reply = await _agent.AskAsync(
            req.Message,
            req.District,
            req.History,
            ct);

        return Ok(new { reply });
    }
}