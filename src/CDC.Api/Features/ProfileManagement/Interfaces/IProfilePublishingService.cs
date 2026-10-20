using CDC.Api.Domain.Common;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileManagement.Interfaces;

/// <summary>
/// Publishes a profile's current draft version as the legacy "Manage profile" page did when "Publish (public)" or
/// "Publish (Defranet only)" was chosen: <c>Profile.Validate()</c>, then <c>Profile.Publish(makePublic)</c>.
/// </summary>
public interface IProfilePublishingService
{
    /// <summary>
    /// Publishes a profile's current draft version. The draft must have no validation errors: when it has any, nothing is
    /// published and the errors are returned. Otherwise the profile's publish rules are checked (the draft exists, the user
    /// is a profile editor, a scenario's parent is published and, for a public scenario, has a public version) before the
    /// new published version is created and the prioritisation scores are recalculated.
    /// </summary>
    /// <param name="profileId">The profile to publish.</param>
    /// <param name="isPublic">Whether the published version is also made public rather than visible to DefraNet only.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The new published version, or the validation errors that stopped it being published; or a not-found, forbidden or
    /// conflict result, carrying the legacy message, when a publish rule is broken.
    /// </returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the draft is not the latest version of the profile or it has no active profiled species.
    /// </exception>
    Task<Result<PublishProfileResultDto>> PublishAsync(Guid profileId, bool isPublic, CancellationToken cancellationToken);
}
