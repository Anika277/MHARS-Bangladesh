namespace MHARS.Web.Models.Agent;

public class AgentChatRequest
{
    public string Message { get; set; } = "";
    public string? District { get; set; }
    public List<AgentChatMessage>? History { get; set; }
}

public class AgentChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}