using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileNotes.Queries;

/// <summary>Retrieves the profile notes recorded against one section of a profile version.</summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
/// <param name="ProfileSectionId">The section to read.</param>
/// <param name="NoteTypeId">The note type to filter by.</param>
public sealed record GetNotesBySectionQuery(Guid ProfileVersionId, Guid ProfileSectionId, Guid NoteTypeId)
    : IRequest<Result<IReadOnlyList<ProfileNoteDto>>>;

/// <summary>Validates <see cref="GetNotesBySectionQuery"/>.</summary>
public sealed class GetNotesBySectionQueryValidator : AbstractValidator<GetNotesBySectionQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetNotesBySectionQueryValidator"/> class.</summary>
    public GetNotesBySectionQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(query => query.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");

        RuleFor(query => query.NoteTypeId)
            .NotEmpty().WithMessage("A note type id is required.");
    }
}

/// <summary>Handles <see cref="GetNotesBySectionQuery"/>.</summary>
/// <param name="profileNoteService">Profile note application service.</param>
public sealed class GetNotesBySectionQueryHandler(IProfileNoteService profileNoteService)
    : IRequestHandler<GetNotesBySectionQuery, Result<IReadOnlyList<ProfileNoteDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching notes; empty when none have been recorded.</returns>
    public async Task<Result<IReadOnlyList<ProfileNoteDto>>> Handle(GetNotesBySectionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var notes = await profileNoteService.GetNotesBySectionAsync(
            request.ProfileVersionId,
            request.ProfileSectionId,
            request.NoteTypeId,
            cancellationToken);

        return Result.Success(notes);
    }
}
