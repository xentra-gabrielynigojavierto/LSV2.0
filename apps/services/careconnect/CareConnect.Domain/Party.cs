namespace CareConnect.Domain;

public class Party
{
    public static class PartyTypeValues
    {
        public const string Individual = "INDIVIDUAL";
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OwnerOrganizationId { get; private set; }
    public string PartyType { get; private set; } = PartyTypeValues.Individual;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? MiddleName { get; private set; }
    public string? PreferredName { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? SsnLast4 { get; private set; }
    public Guid? LinkedUserId { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<PartyContact> Contacts { get; private set; } = [];

    private Party() { }

    public static Party Create(
        Guid tenantId,
        Guid ownerOrganizationId,
        string firstName,
        string lastName,
        string? middleName,
        DateOnly? dateOfBirth,
        string? phone,
        string? email,
        Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var now = DateTime.UtcNow;
        var party = new Party
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OwnerOrganizationId = ownerOrganizationId,
            PartyType = PartyTypeValues.Individual,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            MiddleName = middleName?.Trim(),
            DateOfBirth = dateOfBirth,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (!string.IsNullOrWhiteSpace(phone))
            party.Contacts.Add(PartyContact.Create(party.Id, "PHONE", phone.Trim(), isPrimary: true));

        if (!string.IsNullOrWhiteSpace(email))
            party.Contacts.Add(PartyContact.Create(party.Id, "EMAIL", email.Trim(), isPrimary: true));

        return party;
    }

    public void Update(
        string firstName,
        string lastName,
        string? middleName,
        string? preferredName,
        DateOnly? dateOfBirth,
        string? ssnLast4)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        MiddleName = middleName?.Trim();
        PreferredName = preferredName?.Trim();
        DateOfBirth = dateOfBirth;
        SsnLast4 = ssnLast4?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
