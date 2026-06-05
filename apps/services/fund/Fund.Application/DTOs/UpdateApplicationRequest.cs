namespace Fund.Application.DTOs;

public record UpdateApplicationRequest(
    string   ApplicantFirstName,
    string   ApplicantLastName,
    string   Email,
    string   Phone,
    string   Status,
    decimal? RequestedAmount  = null,
    string?  CaseType         = null,
    string?  IncidentDate     = null,
    string?  AttorneyNotes    = null,
    Guid?    FunderId         = null);
