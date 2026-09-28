using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileNotes.Commands;

/// <summary>Handles <see cref="UpdateNotesCommand"/>.</summary>
/// <param name="profileNoteService">Profile note application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UpdateNotesCommandHandler(
    IProfileNoteService profileNoteService,
    ILogger<UpdateNotesCommandHandler> logger)
    : IRequestHandler<UpdateNotesCommand, Result<ProfileNoteChangesetResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The changeset to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new identifiers and row versions, or a conflict result on a stale update/delete.</returns>
    public async Task<Result<ProfileNoteChangesetResultDto>> Handle(
        UpdateNotesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await profileNoteService.UpdateNotesAsync(request, cancellationToken);

            return Result.Success(result);
        }
        catch (ConcurrencyException exception)
        {
            logger.ConcurrencyConflict(request.ProfileVersionId);

            return Result.Conflict<ProfileNoteChangesetResultDto>(exception.Message);
        }
    }
}
