using FluentValidation;
using Support.Api.Dtos;

namespace Support.Api.Validators;

public class CreateQueueRequestValidator : AbstractValidator<CreateQueueRequest>
{
    public CreateQueueRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.ProductCode).MaximumLength(50);
    }
}

public class UpdateQueueRequestValidator : AbstractValidator<UpdateQueueRequest>
{
    public UpdateQueueRequestValidator()
    {
        RuleFor(x => x.Name!).NotEmpty().MaximumLength(150).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.ProductCode).MaximumLength(50);
    }
}

public class AddQueueMemberRequestValidator : AbstractValidator<AddQueueMemberRequest>
{
    public AddQueueMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public AssignTicketRequestValidator()
    {
        RuleFor(x => x.AssignedUserId).MaximumLength(64);

        RuleFor(x => x)
            .Must(r => r.ClearAssignment == true
                       || !string.IsNullOrWhiteSpace(r.AssignedUserId)
                       || r.AssignedQueueId.HasValue)
            .WithMessage("At least one of assigned_user_id, assigned_queue_id, or clear_assignment must be provided.");

        RuleFor(x => x)
            .Must(r => !(r.ClearAssignment == true
                         && (!string.IsNullOrWhiteSpace(r.AssignedUserId) || r.AssignedQueueId.HasValue)))
            .WithMessage("clear_assignment cannot be combined with assignment fields.");
    }
}
