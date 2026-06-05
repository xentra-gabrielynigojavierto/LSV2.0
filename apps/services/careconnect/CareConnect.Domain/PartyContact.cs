namespace CareConnect.Domain;

public class PartyContact
{
    public static class ContactTypeValues
    {
        public const string Phone          = "PHONE";
        public const string Email          = "EMAIL";
        public const string AlternatePhone = "ALTERNATE_PHONE";
    }

    public Guid Id { get; private set; }
    public Guid PartyId { get; private set; }
    public string ContactType { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Party? Party { get; private set; }

    private PartyContact() { }

    public static PartyContact Create(
        Guid partyId, string contactType, string value, bool isPrimary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new PartyContact
        {
            Id = Guid.NewGuid(),
            PartyId = partyId,
            ContactType = contactType.ToUpperInvariant().Trim(),
            Value = value.Trim(),
            IsPrimary = isPrimary,
            IsVerified = false,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
