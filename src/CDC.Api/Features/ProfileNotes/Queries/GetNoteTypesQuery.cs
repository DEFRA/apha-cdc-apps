using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileNotes.Queries;

/// <summary>Retrieves every profile note type.</summary>
public sealed record GetNoteTypesQuery : IRequest<Result<IReadOnlyList<ProfileNoteTypeDto>>>;

/// <summary>Handles <see cref="GetNoteTypesQuery"/>.</summary>
/// <param name="profileNoteService">Profile note application service.</param>
public sealed class GetNoteTypesQueryHandler(IProfileNoteService profileNoteService)
    : IRequestHandler<GetNoteTypesQuery, Result<IReadOnlyList<ProfileNoteTypeDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every profile note type.</returns>
    public async Task<Result<IReadOnlyList<ProfileNoteTypeDto>>> Handle(GetNoteTypesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var noteTypes = await profileNoteService.GetNoteTypesAsync(cancellationToken);

        return Result.Success(noteTypes);
    }
}
