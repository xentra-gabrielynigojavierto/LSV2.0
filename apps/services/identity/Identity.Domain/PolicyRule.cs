namespace Identity.Domain;

public class PolicyRule
{
    private static readonly HashSet<string> SupportedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "amount", "organizationId", "tenantId", "region", "caseId",
        "owner", "time", "ip", "status", "role", "department"
    };

    public Guid Id { get; private set; }
    public Guid PolicyId { get; private set; }
    public PolicyConditionType ConditionType { get; private set; }
    public string Field { get; private set; } = string.Empty;
    public RuleOperator Operator { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public LogicalGroupType LogicalGroup { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Policy Policy { get; private set; } = null!;

    private PolicyRule() { }

    public static bool IsFieldSupported(string field) =>
        !string.IsNullOrWhiteSpace(field) && SupportedFields.Contains(field.Trim());

    public static PolicyRule Create(
        Guid policyId,
        PolicyConditionType conditionType,
        string field,
        RuleOperator op,
        string value,
        LogicalGroupType logicalGroup = LogicalGroupType.And)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalizedField = field.Trim();
        if (!SupportedFields.Contains(normalizedField))
            throw new ArgumentException(
                $"Field '{normalizedField}' is not a supported attribute field. Supported: {string.Join(", ", SupportedFields)}",
                nameof(field));

        ValidateOperatorForField(normalizedField, op);

        return new PolicyRule
        {
            Id = Guid.NewGuid(),
            PolicyId = policyId,
            ConditionType = conditionType,
            Field = normalizedField,
            Operator = op,
            Value = value.Trim(),
            LogicalGroup = logicalGroup,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(
        PolicyConditionType conditionType,
        string field,
        RuleOperator op,
        string value,
        LogicalGroupType logicalGroup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalizedField = field.Trim();
        if (!SupportedFields.Contains(normalizedField))
            throw new ArgumentException($"Field '{normalizedField}' is not supported.", nameof(field));

        ValidateOperatorForField(normalizedField, op);

        ConditionType = conditionType;
        Field = normalizedField;
        Operator = op;
        Value = value.Trim();
        LogicalGroup = logicalGroup;
    }

    private static void ValidateOperatorForField(string field, RuleOperator op)
    {
        var numericFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "amount", "time" };
        var numericOps = new[] { RuleOperator.GreaterThan, RuleOperator.GreaterThanOrEqual, RuleOperator.LessThan, RuleOperator.LessThanOrEqual };

        if (numericOps.Contains(op) && !numericFields.Contains(field))
            throw new ArgumentException(
                $"Operator '{op}' is only valid for numeric fields ({string.Join(", ", numericFields)}), not '{field}'.",
                nameof(op));
    }

    public static IReadOnlySet<string> GetSupportedFields() => SupportedFields;
}
