using FluentValidation;

namespace CDC.Api.Features.UserAdmin.Commands;

/// <summary>Validates <see cref="UpdateReviewEmailSubscriptionCommand"/>.</summary>
public sealed class UpdateReviewEmailSubscriptionCommandValidator : AbstractValidator<UpdateReviewEmailSubscriptionCommand>
{
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="UpdateReviewEmailSubscriptionCommandValidator"/> class.</summary>
    public UpdateReviewEmailSubscriptionCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage("A user id is required.");

        RuleFor(command => command.LastUpdated)
            .NotNull().WithMessage("The row version read with the user is required.")
            .Must(lastUpdated => lastUpdated.Length == RowVersionLength)
                .WithMessage($"The row version must be {RowVersionLength} bytes.");
    }
}
