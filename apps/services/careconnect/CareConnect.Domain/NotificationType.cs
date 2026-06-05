namespace CareConnect.Domain;

public static class NotificationType
{
    public const string ReferralStatusChanged    = "ReferralStatusChanged";
    public const string ReferralCreated          = "ReferralCreated";
    public const string ReferralProviderAssigned = "ReferralProviderAssigned";
    public const string ReferralAcceptedProvider = "ReferralAcceptedProvider";
    public const string ReferralAcceptedReferrer = "ReferralAcceptedReferrer";
    public const string ReferralAcceptedClient   = "ReferralAcceptedClient";
    public const string ReferralRejectedProvider = "ReferralRejectedProvider";
    public const string ReferralRejectedReferrer = "ReferralRejectedReferrer";
    public const string ReferralCancelledProvider = "ReferralCancelledProvider";
    public const string ReferralCancelledReferrer = "ReferralCancelledReferrer";
    public const string AppointmentScheduled     = "AppointmentScheduled";
    public const string AppointmentConfirmed     = "AppointmentConfirmed";
    public const string AppointmentCancelled     = "AppointmentCancelled";
    public const string AppointmentReminder      = "AppointmentReminder";
    public const string ReferralEmailResent      = "ReferralEmailResent";
    public const string ReferralEmailAutoRetry   = "ReferralEmailAutoRetry";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        ReferralStatusChanged, ReferralCreated, ReferralProviderAssigned,
        ReferralAcceptedProvider, ReferralAcceptedReferrer, ReferralAcceptedClient,
        ReferralRejectedProvider, ReferralRejectedReferrer,
        ReferralCancelledProvider, ReferralCancelledReferrer,
        AppointmentScheduled, AppointmentConfirmed,
        AppointmentCancelled, AppointmentReminder,
        ReferralEmailResent, ReferralEmailAutoRetry,
    };

    public static bool IsValid(string value) => All.Contains(value);
}
