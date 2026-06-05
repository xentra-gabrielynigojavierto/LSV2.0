using FluentValidation;
using Support.Api.Endpoints;

namespace Support.Api.Validators;

/// <summary>
/// Server-side validation for the customer-facing comment endpoint.
///
/// Rules align with the internal CreateCommentRequestValidator:
///   - Body is required (not empty or whitespace-only)
///   - Body max length 8 000 characters
///
/// Author identity (email, name) is sourced from JWT claims in the handler —
/// there are no free-text author fields in this request DTO.
/// </summary>
public class CustomerAddCommentRequestValidator : AbstractValidator<CustomerAddCommentRequest>
{
    public const int MaxBodyLength = 8_000;

    public CustomerAddCommentRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty()
            .WithMessage("Comment body is required.")
            .MaximumLength(MaxBodyLength)
            .WithMessage($"Comment body must not exceed {MaxBodyLength} characters.");
    }
}
