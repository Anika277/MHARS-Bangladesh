namespace MHARS.Web.Models.Agent;

public class GroqOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";
    public string Model { get; set; } = "openai/gpt-oss-120b";
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 800;
}