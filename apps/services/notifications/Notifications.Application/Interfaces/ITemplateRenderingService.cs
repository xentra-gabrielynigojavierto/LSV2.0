namespace Notifications.Application.Interfaces;

public class RenderResult
{
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Text { get; set; }
}

public interface ITemplateRenderingService
{
    RenderResult Render(string? subjectTemplate, string bodyTemplate, string? textTemplate, Dictionary<string, string> data);
    RenderResult RenderBranded(string? subjectTemplate, string bodyTemplate, string? textTemplate, Dictionary<string, string> data, Dictionary<string, string> brandingTokens);
}
