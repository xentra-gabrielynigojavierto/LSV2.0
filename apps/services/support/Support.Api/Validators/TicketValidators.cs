using FluentValidation;
using Support.Api.Dtos;

namespace Support.Api.Validators;

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(8000);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.Severity!).IsInEnum().When(x => x.Severity.HasValue);
        RuleFor(x => x.RequesterEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.RequesterEmail));
        RuleFor(x => x.ProductCode).MaximumLength(50);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.RequesterName).MaximumLength(200);
        RuleFor(x => x.ExternalCustomerEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ExternalCustomerEmail))
            .MaximumLength(320);
        RuleFor(x => x.ExternalCustomerName).MaximumLength(200);
    }
}

public class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x.Title!).NotEmpty().MaximumLength(200).When(x => x.Title is not null);
        RuleFor(x => x.Description).MaximumLength(8000);
        RuleFor(x => x.RequesterEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.RequesterEmail));
        RuleFor(x => x.Status!).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Priority!).IsInEnum().When(x => x.Priority.HasValue);
        RuleFor(x => x.Severity!).IsInEnum().When(x => x.Severity.HasValue);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.RequesterName).MaximumLength(200);
        RuleFor(x => x.DueAt!)
            .Must(d => d >= DateTime.UtcNow.AddMinutes(-1))
            .When(x => x.DueAt.HasValue)
            .WithMessage("due_at cannot be in the past");
    }
}
