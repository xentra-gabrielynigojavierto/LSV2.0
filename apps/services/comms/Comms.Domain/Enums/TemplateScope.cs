namespace Comms.Domain.Enums;

public static class TemplateScope
{
    public const string Global = "GLOBAL";
    public const string Tenant = "TENANT";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Global, Tenant
    };
}
