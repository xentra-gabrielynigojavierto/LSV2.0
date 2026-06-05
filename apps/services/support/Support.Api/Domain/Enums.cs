namespace Support.Api.Domain;

public enum TicketStatus
{
    Open,
    Pending,
    InProgress,
    Resolved,
    Closed,
    Cancelled
}

public enum TicketPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public enum TicketSeverity
{
    Sev4,
    Sev3,
    Sev2,
    Sev1
}

public enum TicketSource
{
    Portal,
    Email,
    Chat,
    Phone,
    Monitoring,
    External
}

public enum ExternalCustomerStatus
{
    Active,
    Inactive
}

public enum TicketRequesterType
{
    InternalUser,
    ExternalCustomer
}

public enum TicketVisibilityScope
{
    Internal,
    CustomerVisible
}

/// <summary>
/// Controls which Support capabilities are active for a specific tenant.
///
/// InternalOnly (default):
///   Internal/admin/agent Support is fully operational.
///   Customer-facing endpoints are disabled and return 403.
///   ExternalCustomer-linked tickets may still be created internally for prep/back-office.
///
/// TenantCustomerSupport:
///   Internal Support is fully operational.
///   Customer-facing endpoints are enabled when CustomerPortalEnabled is also true.
/// </summary>
public enum SupportTenantMode
{
    InternalOnly,
    TenantCustomerSupport
}
