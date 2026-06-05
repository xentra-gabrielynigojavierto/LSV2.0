using FluentValidation;

namespace Documents.Application.DTOs;

public sealed class UpdateDocumentRequest
{
    public string?    Title          { get; init; }
    public string?    Description    { get; init; }
    public Guid?      DocumentTypeId { get; init; }
    public string?    Status         { get; init; }
    public DateTime?  RetainUntil    { get; init; }
}

public sealed class UpdateDocumentRequestValidator : AbstractValidator<UpdateDocumentRequest>
{
    private static readonly HashSet<string> ValidStatuses =
        new() { "DRAFT", "ACTIVE", "ARCHIVED", "LEGAL_HOLD" };

    public UpdateDocumentRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(500).When(x => x.Title is not null);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Status)
            .Must(s => s is null || ValidStatuses.Contains(s))
            .WithMessage("Status must be one of: DRAFT, ACTIVE, ARCHIVED, LEGAL_HOLD");
    }
}
