namespace BuildingBlocks.Authorization;

public class PolicyLoggingOptions
{
    public bool Enabled { get; set; } = true;
    public string AllowLevel { get; set; } = "Debug";
    public string DenyLevel { get; set; } = "Warning";
    public bool LogRuleResultsOnAllow { get; set; }
    public double SampleRate { get; set; } = 1.0;
}
