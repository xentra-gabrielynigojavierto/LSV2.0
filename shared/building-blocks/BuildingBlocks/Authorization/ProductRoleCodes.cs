namespace BuildingBlocks.Authorization;

public static class ProductRoleCodes
{
    // CareConnect
    public const string CareConnectReferrer       = "CARECONNECT_REFERRER";
    public const string CareConnectReceiver       = "CARECONNECT_RECEIVER";
    // CC2-INT-B06: role-based network management (not orgType-based)
    public const string CareConnectNetworkManager = "CARECONNECT_NETWORK_MANAGER";

    // SynqLien
    public const string SynqLienSeller = "SYNQLIEN_SELLER";
    public const string SynqLienBuyer  = "SYNQLIEN_BUYER";
    public const string SynqLienHolder = "SYNQLIEN_HOLDER";

    // SynqFund
    public const string SynqFundReferrer        = "SYNQFUND_REFERRER";
    public const string SynqFundFunder          = "SYNQFUND_FUNDER";
    public const string SynqFundApplicantPortal = "SYNQFUND_APPLICANT_PORTAL";
}
