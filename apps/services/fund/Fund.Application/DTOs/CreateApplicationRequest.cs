namespace Fund.Application.DTOs;

public record CreateApplicationRequest(
    string   ApplicantFirstName,
    string   ApplicantLastName,
    string   Email,
    string   Phone,
    decimal? RequestedAmount  = null,
    string?  CaseType         = null,
    string?  IncidentDate     = null,
    string?  AttorneyNotes    = null,
    Guid?    FunderId         = null);
