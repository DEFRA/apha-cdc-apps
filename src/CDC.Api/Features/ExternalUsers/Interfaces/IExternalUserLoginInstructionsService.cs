using CDC.Api.Domain.Common;
using CDC.Api.Features.ExternalUsers.Commands;
using MediatR;

namespace CDC.Api.Features.ExternalUsers.Interfaces;

/// <summary>Emails an external user the instructions for signing in, and records that they were sent.</summary>
public interface IExternalUserLoginInstructionsService
{
    /// <summary>Sends the login instructions to the address saved on the user, then records them as sent.</summary>
    /// <param name="command">The user to send the instructions to.</param>
    /// <param name="cancellationToken">Cancels the send; once the email has been accepted the record of it is no longer cancellable.</param>
    /// <returns>
    /// Success; not found when the user is not an external user; a conflict when the instructions were already sent or the
    /// address changed during the send; a validation failure when there is no address; or a dependency failure when the
    /// email could not be sent. A failed send leaves the user unsent.
    /// </returns>
    Task<Result<Unit>> SendAsync(SendExternalUserLoginInstructionsCommand command, CancellationToken cancellationToken);
}
