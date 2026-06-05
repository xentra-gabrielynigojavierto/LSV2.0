namespace Liens.Domain.ValueObjects;

public sealed record Address
{
    public string Line1      { get; init; } = string.Empty;
    public string? Line2     { get; init; }
    public string City       { get; init; } = string.Empty;
    public string State      { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string? Country   { get; init; }

    private Address() { }

    public static Address Create(
        string line1,
        string city,
        string state,
        string postalCode,
        string? line2 = null,
        string? country = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line1);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);

        return new Address
        {
            Line1      = line1.Trim(),
            Line2      = line2?.Trim(),
            City       = city.Trim(),
            State      = state.Trim(),
            PostalCode = postalCode.Trim(),
            Country    = country?.Trim(),
        };
    }
}
