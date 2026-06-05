using System.Text.Json;
using FluentValidation;
using Support.Api.Dtos;

namespace Support.Api.Validators;

public class CreateTicketAttachmentRequestValidator : AbstractValidator<CreateTicketAttachmentRequest>
{
    public CreateTicketAttachmentRequestValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).MaximumLength(150);
        RuleFor(x => x.FileSizeBytes!).GreaterThanOrEqualTo(0).When(x => x.FileSizeBytes.HasValue);
        RuleFor(x => x.UploadedByUserId).MaximumLength(64);
    }
}

public class CreateProductReferenceRequestValidator : AbstractValidator<CreateProductReferenceRequest>
{
    public CreateProductReferenceRequestValidator()
    {
        RuleFor(x => x.ProductCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EntityType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EntityId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DisplayLabel).MaximumLength(255);
        RuleFor(x => x.CreatedByUserId).MaximumLength(64);
        RuleFor(x => x.MetadataJson)
            .Must(BeValidJson)
            .When(x => !string.IsNullOrWhiteSpace(x.MetadataJson))
            .WithMessage("metadata_json must be valid JSON");
    }

    private static bool BeValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try { using var _ = JsonDocument.Parse(json); return true; }
        catch { return false; }
    }
}
