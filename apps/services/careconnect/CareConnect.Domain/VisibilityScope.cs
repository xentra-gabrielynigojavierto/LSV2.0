namespace CareConnect.Domain;

public static class VisibilityScope
{
    /// <summary>Visible only to the organization that created the note or attachment.</summary>
    public const string Internal = "INTERNAL";

    /// <summary>Visible to all workflow participants (referring and receiving orgs).</summary>
    public const string Shared = "SHARED";
}
