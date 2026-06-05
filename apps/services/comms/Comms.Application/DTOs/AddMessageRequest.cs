namespace Comms.Application.DTOs;

public record AddMessageRequest(
    string Body,
    string Channel,
    string Direction,
    string VisibilityType);
