using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileReports.Interfaces;

namespace CDC.Api.Application;

/// <summary>
/// Looks up the facts about a profile version and refuses a request that the acting user's role does not allow. A refusal
/// is an <see cref="UnauthorizedAccessException"/>, which the global exception handler turns into a 403.
/// </summary>
public interface IProfileVersionGuard
{
    /// <summary>Reads the facts about a profile version.</summary>
    /// <param name="profileVersionId">The version.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The facts, or <see langword="null"/> when the version does not exist.</returns>
    Task<ProfileVersionFacts?> FindAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Reads the facts about a profile version that must exist.</summary>
    /// <param name="profileVersionId">The version.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The facts.</returns>
    /// <exception cref="NotFoundException">The version does not exist.</exception>
    Task<ProfileVersionFacts> RequireAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>
    /// Refuses a request to read a draft version the acting user may not view. A published version, and a version that does
    /// not exist, are never refused here: the first is open to everyone signed in and the second has nothing to protect.
    /// </summary>
    /// <param name="profileVersionId">The version.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>A task that completes when the request is allowed.</returns>
    /// <exception cref="UnauthorizedAccessException">The version is a draft the user may not view.</exception>
    Task EnsureCanViewAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>
    /// Refuses a request to browse one version of a profile when it is a draft the acting user may not view, as the legacy site
    /// refused a user who opened a draft they held no role on. A published version is open to everyone signed in.
    /// </summary>
    /// <param name="profileId">The profile the version is browsed under.</param>
    /// <param name="profileVersionId">The version.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>A task that completes when the request is allowed.</returns>
    /// <exception cref="NotFoundException">The version does not exist, or is not a version of the profile.</exception>
    /// <exception cref="UnauthorizedAccessException">The version is a draft the user may not view.</exception>
    Task EnsureCanBrowseAsync(Guid profileId, Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>
    /// Refuses a request to read the notes or review comments of a version when the acting user may not view the profile's
    /// drafts. They are working papers, so a published version's are no more open than a draft's.
    /// </summary>
    /// <param name="profileVersionId">The version.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>A task that completes when the request is allowed.</returns>
    /// <exception cref="UnauthorizedAccessException">The user may not view the profile's drafts.</exception>
    Task EnsureCanViewNotesAsync(Guid profileVersionId, CancellationToken cancellationToken);
}

/// <summary>Default <see cref="IProfileVersionGuard"/>, reading the version through <see cref="IProfileReportRepository"/>.</summary>
/// <param name="repository">Reads a version's header.</param>
/// <param name="userContext">The acting user.</param>
public sealed class ProfileVersionGuard(IProfileReportRepository repository, IUserContext userContext) : IProfileVersionGuard
{
    /// <inheritdoc />
    public async Task<ProfileVersionFacts?> FindAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var header = await repository.GetProfileVersionHeaderAsync(profileVersionId, cancellationToken);

        return header is null ? null : ProfileVersionFacts.From(header);
    }

    /// <inheritdoc />
    public async Task<ProfileVersionFacts> RequireAsync(Guid profileVersionId, CancellationToken cancellationToken) =>
        await FindAsync(profileVersionId, cancellationToken) ?? throw NotFoundException.For("Profile version", profileVersionId);

    /// <inheritdoc />
    public async Task EnsureCanViewAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        if (await FindAsync(profileVersionId, cancellationToken) is { } version && !ProfileAccess.CanView(userContext, version))
        {
            throw new UnauthorizedAccessException("The acting user may not view this draft profile version.");
        }
    }

    /// <inheritdoc />
    public async Task EnsureCanBrowseAsync(Guid profileId, Guid profileVersionId, CancellationToken cancellationToken)
    {
        var version = await RequireAsync(profileVersionId, cancellationToken);

        if (version.ProfileId != profileId)
        {
            throw NotFoundException.For("Profile version", profileVersionId);
        }

        if (!ProfileAccess.CanView(userContext, version))
        {
            throw new UnauthorizedAccessException("The acting user may not view this draft profile version.");
        }
    }

    /// <inheritdoc />
    public async Task EnsureCanViewNotesAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        if (await FindAsync(profileVersionId, cancellationToken) is { } version && !ProfileAccess.CanViewDraft(userContext, version.ProfileId))
        {
            throw new UnauthorizedAccessException("The acting user may not view the notes and review comments of this profile.");
        }
    }
}
