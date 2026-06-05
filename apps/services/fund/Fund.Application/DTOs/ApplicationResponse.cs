namespace Fund.Application.DTOs;

public record ApplicationResponse(
    Guid      Id,
    Guid      TenantId,
    string    ApplicationNumber,
    string    ApplicantFirstName,
    string    ApplicantLastName,
    string    Email,
    string    Phone,
    decimal?  RequestedAmount,
    decimal?  ApprovedAmount,
    string?   CaseType,
    string?   IncidentDate,
    string?   AttorneyNotes,
    string?   ApprovalTerms,
    string?   DenialReason,
    Guid?     FunderId,
    string    Status,
    Guid?     CreatedByUserId,
    Guid?     UpdatedByUserId,
    DateTime  CreatedAtUtc,
    DateTime  UpdatedAtUtc);
