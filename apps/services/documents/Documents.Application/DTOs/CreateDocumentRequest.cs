using FluentValidation;

namespace Documents.Application.DTOs;

public sealed class CreateDocumentRequest
{
    public Guid    TenantId       { get; init; }
    public string  ProductId      { get; init; } = string.Empty;
    public string  ReferenceId    { get; init; } = string.Empty;
    public string  ReferenceType  { get; init; } = string.Empty;
    public Guid    DocumentTypeId { get; init; }
    public string  Title          { get; init; } = string.Empty;
    public string? Description    { get; init; }
}

public sealed class CreateDocumentRequestValidator : AbstractValidator<CreateDocumentRequest>
{
    public CreateDocumentRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ReferenceId).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ReferenceType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DocumentTypeId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
    }
}
