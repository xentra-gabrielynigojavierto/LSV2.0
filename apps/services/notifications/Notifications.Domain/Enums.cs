namespace Notifications.Domain;

public enum NotificationChannel
{
    Email,
    Sms,
    Push,
    InApp
}

public enum NotificationStatus
{
    Accepted,
    Processing,
    Sent,
    Failed,
    Blocked
}

public enum FailureCategory
{
    RetryableProviderFailure,
    NonRetryableFailure,
    ProviderUnavailable,
    InvalidRecipient,
    AuthConfigFailure
}

public enum ProviderHealthStatus
{
    Healthy,
    Degraded,
    Down
}

public enum ProviderOwnershipMode
{
    Platform,
    Tenant
}

public enum TenantProviderValidationStatus
{
    NotValidated,
    Valid,
    Invalid
}

public enum TenantProviderHealthStatus
{
    Healthy,
    Degraded,
    Down,
    Unknown
}

public enum TenantChannelProviderMode
{
    PlatformManaged,
    TenantManaged
}

public enum TenantProviderConfigStatus
{
    Active,
    Inactive
}

public enum SuppressionType
{
    Manual,
    Bounce,
    Unsubscribe,
    Complaint,
    InvalidContact,
    CarrierRejection,
    SystemProtection
}

public enum SuppressionSource
{
    ProviderWebhook,
    ManualAdmin,
    SystemRule,
    Import
}

public enum SuppressionStatus
{
    Active,
    Expired,
    Lifted
}

public enum BillingMode
{
    UsageBased,
    FlatRate,
    Hybrid
}

public enum BillingPlanStatus
{
    Active,
    Inactive,
    Archived
}

public enum RateLimitPolicyStatus
{
    Active,
    Inactive
}

public enum UsageUnit
{
    ApiNotificationRequest,
    ApiNotificationRequestRejected,
    EmailAttempt,
    EmailSent,
    SmsAttempt,
    SmsSent,
    ProviderFailoverAttempt,
    TemplateRender,
    SuppressedNotificationRequestRejected
}

public enum NormalizedEventType
{
    Accepted,
    Queued,
    Deferred,
    Sent,
    Delivered,
    Bounced,
    Failed,
    Undeliverable,
    Rejected,
    Complained,
    Unsubscribed,
    Opened,
    Clicked
}

public enum DeliveryIssueType
{
    BouncedEmail,
    InvalidEmail,
    SmsUndelivered,
    InvalidPhone,
    UnsubscribedRecipient,
    ComplainedRecipient,
    OptedOutRecipient,
    ProviderRejected
}

public enum ProductType
{
    CareConnect,
    SynqLien,
    SynqFund,
    SynqRx,
    SynqPayout
}

public enum TemplateScope
{
    Global,
    Tenant
}

public enum EditorType
{
    Wysiwyg,
    Html,
    Text
}

public enum AttemptStatus
{
    Pending,
    Sending,
    Sent,
    Failed
}

public enum ContactHealthStatus
{
    Valid,
    Bounced,
    Complained,
    Unsubscribed,
    Suppressed,
    Invalid,
    CarrierRejected,
    OptedOut,
    Unreachable
}

public static class EnumMappings
{
    public static readonly SuppressionType[] NonOverrideableSuppressionTypes =
    {
        SuppressionType.Unsubscribe,
        SuppressionType.Complaint,
        SuppressionType.SystemProtection
    };

    public static string ToSnakeCase(this NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => "email",
        NotificationChannel.Sms => "sms",
        NotificationChannel.Push => "push",
        NotificationChannel.InApp => "in-app",
        _ => channel.ToString().ToLowerInvariant()
    };

    public static NotificationChannel ParseChannel(string value) => value.ToLowerInvariant() switch
    {
        "email" => NotificationChannel.Email,
        "sms" => NotificationChannel.Sms,
        "push" => NotificationChannel.Push,
        "in-app" or "inapp" => NotificationChannel.InApp,
        _ => throw new ArgumentException($"Invalid channel: {value}")
    };

    public static string ToSnakeCase(this NotificationStatus status) => status switch
    {
        NotificationStatus.Accepted => "accepted",
        NotificationStatus.Processing => "processing",
        NotificationStatus.Sent => "sent",
        NotificationStatus.Failed => "failed",
        NotificationStatus.Blocked => "blocked",
        _ => status.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this FailureCategory category) => category switch
    {
        FailureCategory.RetryableProviderFailure => "retryable_provider_failure",
        FailureCategory.NonRetryableFailure => "non_retryable_failure",
        FailureCategory.ProviderUnavailable => "provider_unavailable",
        FailureCategory.InvalidRecipient => "invalid_recipient",
        FailureCategory.AuthConfigFailure => "auth_config_failure",
        _ => category.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this SuppressionType type) => type switch
    {
        SuppressionType.Manual => "manual",
        SuppressionType.Bounce => "bounce",
        SuppressionType.Unsubscribe => "unsubscribe",
        SuppressionType.Complaint => "complaint",
        SuppressionType.InvalidContact => "invalid_contact",
        SuppressionType.CarrierRejection => "carrier_rejection",
        SuppressionType.SystemProtection => "system_protection",
        _ => type.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this UsageUnit unit) => unit switch
    {
        UsageUnit.ApiNotificationRequest => "api_notification_request",
        UsageUnit.ApiNotificationRequestRejected => "api_notification_request_rejected",
        UsageUnit.EmailAttempt => "email_attempt",
        UsageUnit.EmailSent => "email_sent",
        UsageUnit.SmsAttempt => "sms_attempt",
        UsageUnit.SmsSent => "sms_sent",
        UsageUnit.ProviderFailoverAttempt => "provider_failover_attempt",
        UsageUnit.TemplateRender => "template_render",
        UsageUnit.SuppressedNotificationRequestRejected => "suppressed_notification_request_rejected",
        _ => unit.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this NormalizedEventType type) => type switch
    {
        NormalizedEventType.Accepted => "accepted",
        NormalizedEventType.Queued => "queued",
        NormalizedEventType.Deferred => "deferred",
        NormalizedEventType.Sent => "sent",
        NormalizedEventType.Delivered => "delivered",
        NormalizedEventType.Bounced => "bounced",
        NormalizedEventType.Failed => "failed",
        NormalizedEventType.Undeliverable => "undeliverable",
        NormalizedEventType.Rejected => "rejected",
        NormalizedEventType.Complained => "complained",
        NormalizedEventType.Unsubscribed => "unsubscribed",
        NormalizedEventType.Opened => "opened",
        NormalizedEventType.Clicked => "clicked",
        _ => type.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this DeliveryIssueType type) => type switch
    {
        DeliveryIssueType.BouncedEmail => "bounced_email",
        DeliveryIssueType.InvalidEmail => "invalid_email",
        DeliveryIssueType.SmsUndelivered => "sms_undelivered",
        DeliveryIssueType.InvalidPhone => "invalid_phone",
        DeliveryIssueType.UnsubscribedRecipient => "unsubscribed_recipient",
        DeliveryIssueType.ComplainedRecipient => "complained_recipient",
        DeliveryIssueType.OptedOutRecipient => "opted_out_recipient",
        DeliveryIssueType.ProviderRejected => "provider_rejected",
        _ => type.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this ProductType type) => type switch
    {
        ProductType.CareConnect => "careconnect",
        ProductType.SynqLien => "synqlien",
        ProductType.SynqFund => "synqfund",
        ProductType.SynqRx => "synqrx",
        ProductType.SynqPayout => "synqpayout",
        _ => type.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this AttemptStatus status) => status switch
    {
        AttemptStatus.Pending => "pending",
        AttemptStatus.Sending => "sending",
        AttemptStatus.Sent => "sent",
        AttemptStatus.Failed => "failed",
        _ => status.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this ProviderOwnershipMode mode) => mode switch
    {
        ProviderOwnershipMode.Platform => "platform",
        ProviderOwnershipMode.Tenant => "tenant",
        _ => mode.ToString().ToLowerInvariant()
    };

    public static string ToSnakeCase(this TenantChannelProviderMode mode) => mode switch
    {
        TenantChannelProviderMode.PlatformManaged => "platform_managed",
        TenantChannelProviderMode.TenantManaged => "tenant_managed",
        _ => mode.ToString().ToLowerInvariant()
    };
}
