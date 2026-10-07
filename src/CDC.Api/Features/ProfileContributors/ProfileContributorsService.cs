using System.Text.RegularExpressions;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
using CDC.Api.Features.ProfileContributors.Mapping;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileContributors;

/// <summary>
/// Default <see cref="IProfileContributorsService"/>: reads through
/// <see cref="IProfileContributorsRepository"/>, pages the result in memory (the backing stored
/// procedure has no paging parameters), and maps domain entities onto the DTOs the API returns.
/// </summary>
/// <param name="repository">Profile contributors data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed partial class ProfileContributorsService(IProfileContributorsRepository repository, ILogger<ProfileContributorsService> logger)
    : IProfileContributorsService
{
    /// <summary>Matches a legacy-style domain-qualified username (e.g. <c>DOMAIN\username</c>).</summary>
    [GeneratedRegex(@"^\w+([-.]\w+)*\\[\w-]+([\. ][\w-]+)*")]
    private static partial Regex UsernameFormatRegex();

    /// <inheritdoc />
    public async Task<PagedResult<ContributorDto>> GetProfileContributorsAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var contributors = await repository.GetProfileContributorsAsync(profileId, cancellationToken);
        logger.RetrievedProfileContributors(contributors.Count, profileId);

        var effectivePageNumber = Math.Max(1, pageNumber);
        var effectivePageSize = Math.Max(0, pageSize);

        var page = effectivePageSize > 0
            ? contributors.Skip((effectivePageNumber - 1) * effectivePageSize).Take(effectivePageSize)
            : contributors;

        return new PagedResult<ContributorDto>
        {
            Items = [.. page.Select(contributor => contributor.ToDto())],
            PageNumber = effectivePageNumber,
            PageSize = effectivePageSize,
            TotalRecords = contributors.Count
        };
    }

    /// <inheritdoc />
    public async Task<ContributorEditDto?> GetContributorForEditAsync(Guid profileId, Guid contributorId, CancellationToken cancellationToken)
    {
        var contributor = await repository.GetContributorForEditAsync(profileId, contributorId, cancellationToken);

        if (contributor is null)
        {
            logger.ContributorForEditNotFound(contributorId, profileId);
            return null;
        }

        logger.RetrievedContributorForEdit(contributorId, profileId);

        return contributor.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileUserRoleDto>> GetProfileUserRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await repository.GetProfileUserRolesAsync(cancellationToken);
        logger.RetrievedProfileUserRoles(roles.Count);

        return [.. roles.Select(role => role.ToDto())];
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> UpdateContributorAsync(UpdateContributorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var contributor = await repository.GetContributorForEditAsync(command.ProfileId, command.ContributorId, cancellationToken);

        if (contributor is null)
        {
            return Result.NotFound<Unit>(
                $"Contributor '{command.ContributorId}' was not found on profile '{command.ProfileId}'.");
        }

        var validationError = await ValidateAsync(
            command.RoleId, command.FullName, command.Organisation, command.SectionPermissionIds, contributor.IsSsoUser, cancellationToken);

        if (validationError is not null)
        {
            return Result.ValidationFailed<Unit>(validationError);
        }

        logger.UpdatingContributor(command.ContributorId, command.ProfileId);

        try
        {
            var newLastUpdated = await repository.UpdateContributorAsync(command, cancellationToken);

            if (newLastUpdated is null)
            {
                return Result.NotFound<Unit>(
                    $"Contributor '{command.ContributorId}' was not found on profile '{command.ProfileId}'.");
            }

            logger.UpdatedContributor(command.ContributorId, command.ProfileId);

            return Result.Success(Unit.Value);
        }
        catch (ConcurrencyException exception)
        {
            logger.ContributorConcurrencyConflict(command.ContributorId);

            return Result.Conflict<Unit>(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<UserVerificationResultDto> VerifyUsernameAsync(string userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userName);

        var user = await repository.FindUserByUsernameAsync(userName, cancellationToken);

        if (user is not null)
        {
            if (user.IsUserManagementSystem)
            {
                logger.UsernameBlocked();

                return new UserVerificationResultDto { Outcome = UserVerificationOutcome.Blocked };
            }

            logger.UsernameMatchedExistingUser(user.Id);

            return new UserVerificationResultDto { Outcome = UserVerificationOutcome.ExistingUser, UserId = user.Id };
        }

        if (UsernameFormatRegex().IsMatch(userName))
        {
            logger.UsernameIsNew();

            return new UserVerificationResultDto { Outcome = UserVerificationOutcome.NewUser };
        }

        logger.UsernameInvalidFormat();

        return new UserVerificationResultDto { Outcome = UserVerificationOutcome.InvalidFormat };
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> AddContributorAsync(AddContributorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationError = await ValidateAsync(
            command.RoleId, command.FullName, command.Organisation, command.SectionPermissionIds, command.IsSsoUser, cancellationToken);

        if (validationError is not null)
        {
            return Result.ValidationFailed<Unit>(validationError);
        }

        logger.AddingContributor(command.ContributorId, command.ProfileId);

        try
        {
            await repository.AddContributorAsync(command, cancellationToken);

            logger.AddedContributor(command.ContributorId, command.ProfileId);

            return Result.Success(Unit.Value);
        }
        catch (ConcurrencyException exception)
        {
            logger.ContributorConcurrencyConflict(command.ContributorId);

            return Result.Conflict<Unit>(exception.Message);
        }
        catch (DuplicateUsernameException exception)
        {
            logger.DuplicateUsernameConflict(command.ContributorId);

            return Result.ValidationFailed<Unit>(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeleteContributorAsync(DeleteContributorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger.DeletingContributor(command.ContributorId, command.ProfileId);

        try
        {
            await repository.DeleteContributorAsync(command.ProfileId, command.ContributorId, command.LastUpdated, cancellationToken);

            logger.DeletedContributor(command.ContributorId, command.ProfileId);

            return Result.Success(Unit.Value);
        }
        catch (ConcurrencyException exception)
        {
            logger.ContributorConcurrencyConflict(command.ContributorId);

            return Result.Conflict<Unit>(exception.Message);
        }
    }

    /// <summary>Replicates the legacy <c>ProfileContributor</c> business rules. Returns the first
    /// validation failure message, or <see langword="null"/> when the change is valid.</summary>
    private async Task<string?> ValidateAsync(
        Guid roleId, string fullName, string organisation, IReadOnlyList<Guid> sectionPermissionIds, bool isSsoUser, CancellationToken cancellationToken)
    {
        var roles = await repository.GetProfileUserRolesAsync(cancellationToken);
        var role = roles.FirstOrDefault(candidate => candidate.Id == roleId);

        if (role is null)
        {
            return "Please select a valid role.";
        }

        if (role.IsContributor && sectionPermissionIds.Count == 0)
        {
            return "A contributor must have at least one profile section permission granted.";
        }

        if (!role.IsContributor && sectionPermissionIds.Count > 0)
        {
            return "A reviewer must not have any profile section permissions granted.";
        }

        if (!isSsoUser)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return "Please enter a full name.";
            }

            if (string.IsNullOrWhiteSpace(organisation))
            {
                return "Please enter an organisation.";
            }
        }

        return null;
    }
}
