using MHARS.Web.Models.Agent;

namespace MHARS.Web.Services;

public interface IGroqAgentService
{
    Task<string> AskAsync(
        string message,
        string? district,
        List<AgentChatMessage>? history,
        CancellationToken ct = default);
}