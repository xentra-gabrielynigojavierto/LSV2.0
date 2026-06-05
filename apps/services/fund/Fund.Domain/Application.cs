using BuildingBlocks.Domain;

namespace Fund.Domain;

public class Application : AuditableEntity
{
    public static readonly IReadOnlyCollection<string> ValidStatuses =
        new[] { "Draft", "Submitted", "InReview", "Approved", "Rejected" };

    public Guid   Id                { get; private set; }
    public Guid   TenantId          { get; private set; }
    public string ApplicationNumber { get; private set; } = string.Empty;

    // Applicant (subject party — inline for Phase 1 compatibility)
    public string ApplicantFirstName { get; private set; } = string.Empty;
    public string ApplicantLastName  { get; private set; } = string.Empty;
    public string Email              { get; private set; } = string.Empty;
    public string Phone              { get; private set; } = string.Empty;

    // Funding request
    public decimal? RequestedAmount  { get; private set; }
    public decimal? ApprovedAmount   { get; private set; }
    public string?  CaseType         { get; private set; }
    public string?  IncidentDate     { get; private set; }   // yyyy-MM-dd string
    public string?  AttorneyNotes    { get; private set; }
    public string?  ApprovalTerms    { get; private set; }
    public string?  DenialReason     { get; private set; }

    // Org routing
    public Guid? FunderId { get; private set; }

    public string Status { get; private set; } = "Draft";

    private Application() { }

    public static Application Create(
        Guid   tenantId,
        string applicationNumber,
        string applicantFirstName,
        string applicantLastName,
        string email,
        string phone,
        Guid   createdByUserId,
        decimal? requestedAmount  = null,
        string?  caseType         = null,
        string?  incidentDate     = null,
        string?  attorneyNotes    = null,
        Guid?    funderId         = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantFirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantLastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        var now = DateTime.UtcNow;
        return new Application
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            ApplicationNumber = applicationNumber,
            ApplicantFirstName = applicantFirstName.Trim(),
            ApplicantLastName  = applicantLastName.Trim(),
            Email              = email.ToLowerInvariant().Trim(),
            Phone              = phone.Trim(),
            RequestedAmount    = requestedAmount,
            CaseType           = caseType?.Trim(),
            IncidentDate       = incidentDate?.Trim(),
            AttorneyNotes      = attorneyNotes?.Trim(),
            FunderId           = funderId,
            Status             = "Draft",
            CreatedByUserId    = createdByUserId,
            UpdatedByUserId    = createdByUserId,
            CreatedAtUtc       = now,
            UpdatedAtUtc       = now,
        };
    }

    // ── Inline field update (law firm edits a Draft) ─────────────────────────

    public void Update(
        string   applicantFirstName,
        string   applicantLastName,
        string   email,
        string   phone,
        string   status,
        Guid     updatedByUserId,
        decimal? requestedAmount = null,
        string?  caseType        = null,
        string?  incidentDate    = null,
        string?  attorneyNotes   = null,
        Guid?    funderId        = null)
    {
        ApplicantFirstName = applicantFirstName.Trim();
        ApplicantLastName  = applicantLastName.Trim();
        Email              = email.ToLowerInvariant().Trim();
        Phone              = phone.Trim();
        Status             = status;
        RequestedAmount    = requestedAmount;
        CaseType           = caseType?.Trim();
        IncidentDate       = incidentDate?.Trim();
        AttorneyNotes      = attorneyNotes?.Trim();
        FunderId           = funderId;
        UpdatedByUserId    = updatedByUserId;
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>Draft → Submitted.  Law firm submits to a funder org.</summary>
    public void Submit(Guid? funderId, Guid updatedByUserId)
    {
        if (Status != "Draft")
            throw new InvalidOperationException($"Cannot submit an application with status '{Status}'.");

        FunderId        = funderId ?? FunderId;
        Status          = "Submitted";
        UpdatedByUserId = updatedByUserId;
    }

    /// <summary>Submitted → InReview.  Funder begins review.</summary>
    public void BeginReview(Guid updatedByUserId)
    {
        if (Status != "Submitted")
            throw new InvalidOperationException($"Cannot begin review on application with status '{Status}'.");

        Status          = "InReview";
        UpdatedByUserId = updatedByUserId;
    }

    /// <summary>InReview → Approved.  Funder approves with an amount.</summary>
    public void Approve(decimal approvedAmount, string? approvalTerms, Guid updatedByUserId)
    {
        if (Status != "InReview")
            throw new InvalidOperationException($"Cannot approve an application with status '{Status}'.");

        ApprovedAmount  = approvedAmount;
        ApprovalTerms   = approvalTerms?.Trim();
        Status          = "Approved";
        UpdatedByUserId = updatedByUserId;
    }

    /// <summary>InReview → Rejected.  Funder denies with a reason.</summary>
    public void Deny(string reason, Guid updatedByUserId)
    {
        if (Status != "InReview")
            throw new InvalidOperationException($"Cannot deny an application with status '{Status}'.");

        DenialReason    = reason.Trim();
        Status          = "Rejected";
        UpdatedByUserId = updatedByUserId;
    }
}
