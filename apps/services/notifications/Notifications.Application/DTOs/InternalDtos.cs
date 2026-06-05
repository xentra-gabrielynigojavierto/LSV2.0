namespace Notifications.Application.DTOs;

public class InternalSendEmailDto
{
    public string To { get; set; } = string.Empty;
    public string? From { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Html { get; set; }
    public string? ReplyTo { get; set; }
}

public class InternalSendEmailResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
