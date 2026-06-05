namespace Fund.Application.DTOs;

public record ApproveApplicationRequest(
    decimal  ApprovedAmount,
    string?  ApprovalTerms = null);
