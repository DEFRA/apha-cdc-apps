using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileNotes.Queries;

/// <summary>Retrieves every profile note recorded against a profile version.</summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
/// <param name="NoteTypeId">The note type to filter by.</param>
public sealed record GetNotesByVersionQuery(Guid ProfileVersionId, Guid NoteTypeId)
    : IRequest<Result<IReadOnlyList<ProfileNoteDto>>>;

/// <summary>Validates <see cref="GetNotesByVersionQuery"/>.</summary>
public sealed class GetNotesByVersionQueryValidator : AbstractValidator<GetNotesByVersionQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetNotesByVersionQueryValidator"/> class.</summary>
    public GetNotesByVersionQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(query => query.NoteTypeId)
            .NotEmpty().WithMessage("A note type id is required.");
    }
}

/// <summary>Handles <see cref="GetNotesByVersionQuery"/>.</summary>
/// <param name="profileNoteService">Profile note application service.</param>
public sealed class GetNotesByVersionQueryHandler(IProfileNoteService profileNoteService)
    : IRequestHandler<GetNotesByVersionQuery, Result<IReadOnlyList<ProfileNoteDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching notes; empty when none have been recorded.</returns>
    public async Task<Result<IReadOnlyList<ProfileNoteDto>>> Handle(GetNotesByVersionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var notes = await profileNoteService.GetNotesByVersionAsync(request.ProfileVersionId, request.NoteTypeId, cancellationToken);

        return Result.Success(notes);
    }
}
