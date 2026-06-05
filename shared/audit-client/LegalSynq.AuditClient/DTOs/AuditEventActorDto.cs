using LegalSynq.AuditClient.Enums;

namespace LegalSynq.AuditClient.DTOs;

public sealed class AuditEventActorDto
{
    public string?    Id        { get; set; }
    public ActorType  Type      { get; set; } = ActorType.User;
    public string?    Name      { get; set; }
    public string?    IpAddress { get; set; }
    public string?    UserAgent { get; set; }
}
