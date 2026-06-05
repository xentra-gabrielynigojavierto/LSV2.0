namespace Identity.Infrastructure.Services;

public sealed class VerificationRetryOptions
{
    public int MaxAttempts { get; set; } = 5;
    public int InitialDelaySeconds { get; set; } = 30;
    public int MaxDelaySeconds { get; set; } = 300;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int MaxRetryWindowMinutes { get; set; } = 30;

    public int ComputeDelaySeconds(int attemptNumber)
    {
        if (attemptNumber <= 1) return InitialDelaySeconds;
        var delay = InitialDelaySeconds * Math.Pow(BackoffMultiplier, attemptNumber - 1);
        return (int)Math.Min(delay, MaxDelaySeconds);
    }
}
