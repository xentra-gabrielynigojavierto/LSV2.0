using FluentValidation;
using Support.Api.Dtos;

namespace Support.Api.Validators;

public class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.CommentType!).IsInEnum().When(x => x.CommentType.HasValue);
        RuleFor(x => x.Visibility!).IsInEnum().When(x => x.Visibility.HasValue);
        RuleFor(x => x.AuthorEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.AuthorEmail));
        RuleFor(x => x.AuthorUserId).MaximumLength(64);
        RuleFor(x => x.AuthorName).MaximumLength(200);
    }
}
