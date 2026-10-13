using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Commands;

/// <summary>
/// Saves the "All Revision dates" tab of one profile version, matching the legacy
/// <c>ProfileVersionSection.Save()</c> pathway for the revision fields (frequencies, next review
/// dates and review statuses).
/// </summary>
public sealed record SaveProfileVersionRevisionDatesCommand : IRequest<Result<SaveProfileRevisionDatesResultDto>>
{
    /// <summary>Gets the profile version being edited.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the submitted revision data of each section.</summary>
    public required IReadOnlyList<ProfileSectionRevisionDatesInputDto> Sections { get; init; }
}

/// <summary>Validates <see cref="SaveProfileVersionRevisionDatesCommand"/>.</summary>
public sealed class SaveProfileVersionRevisionDatesCommandValidator : AbstractValidator<SaveProfileVersionRevisionDatesCommand>
{
    /// <summary>The earliest date the legacy page accepts (<c>smalldatetime</c> lower bound).</summary>
    public static readonly DateTime MinimumReviewDate = new(1900, 1, 1);

    /// <summary>The latest date the legacy page accepts (<c>smalldatetime</c> upper bound).</summary>
    public static readonly DateTime MaximumReviewDate = new(2079, 6, 6);

    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="SaveProfileVersionRevisionDatesCommandValidator"/> class.</summary>
    public SaveProfileVersionRevisionDatesCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(command => command.Sections)
            .NotEmpty().WithMessage("At least one section is required.");

        RuleFor(command => command.Sections)
            .Must(sections => sections.Select(section => section.ProfileSectionId).Distinct().Count() == sections.Count)
            .WithMessage("Each section may be submitted only once.")
            .When(command => command.Sections is { Count: > 0 });

        RuleForEach(command => command.Sections).ChildRules(section =>
        {
            section.RuleFor(item => item.ProfileSectionId)
                .NotEmpty().WithMessage("A profile section id is required.");

            section.RuleFor(item => item.LastUpdated)
                .NotNull().Must(value => value is { Length: RowVersionLength })
                .WithMessage("The section's row version is required.");

            section.RuleFor(item => item.NextAuthorReview).Must(BeWithinSupportedRange)
                .WithMessage($"Next author review date must be between {MinimumReviewDate:d MMMM yyyy} and {MaximumReviewDate:d MMMM yyyy}.");
            section.RuleFor(item => item.NextVaReview).Must(BeWithinSupportedRange)
                .WithMessage($"Next VA review date must be between {MinimumReviewDate:d MMMM yyyy} and {MaximumReviewDate:d MMMM yyyy}.");
            section.RuleFor(item => item.NextPolicyReview).Must(BeWithinSupportedRange)
                .WithMessage($"Next policy review date must be between {MinimumReviewDate:d MMMM yyyy} and {MaximumReviewDate:d MMMM yyyy}.");

            section.RuleFor(item => item.AuthorReviewFrequencyId).NotEqual(Guid.Empty);
            section.RuleFor(item => item.VaReviewFrequencyId).NotEqual(Guid.Empty);
            section.RuleFor(item => item.PolicyReviewFrequencyId).NotEqual(Guid.Empty);
            section.RuleFor(item => item.AuthorReviewStatusId).NotEqual(Guid.Empty);
            section.RuleFor(item => item.VaReviewStatusId).NotEqual(Guid.Empty);
            section.RuleFor(item => item.PolicyReviewStatusId).NotEqual(Guid.Empty);
        });
    }

    private static bool BeWithinSupportedRange(DateTime? date) =>
        date is null || (date.Value.Date >= MinimumReviewDate && date.Value.Date <= MaximumReviewDate);
}

/// <summary>
/// Handles <see cref="SaveProfileVersionRevisionDatesCommand"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class SaveProfileVersionRevisionDatesCommandHandler(
    IProfileSectionService profileSectionService,
    ILogger<SaveProfileVersionRevisionDatesCommandHandler> logger)
    : IRequestHandler<SaveProfileVersionRevisionDatesCommand, Result<SaveProfileRevisionDatesResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of sections written, or a conflict result when a section was edited by
    /// another user or the profile version is not an editable current draft.</returns>
    public async Task<Result<SaveProfileRevisionDatesResultDto>> Handle(
        SaveProfileVersionRevisionDatesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await profileSectionService.SaveProfileVersionRevisionDatesAsync(
                request.ProfileVersionId,
                request.Sections,
                cancellationToken);

            return Result.Success(result);
        }
        catch (ConcurrencyException exception)
        {
            logger.RevisionDatesConcurrencyConflict(request.ProfileVersionId);

            return Result.Conflict<SaveProfileRevisionDatesResultDto>(exception.Message);
        }
    }
}
